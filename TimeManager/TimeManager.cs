using System;
using System.Collections;
using Pxp.Pattern;

public sealed class TimeManager : Singleton<TimeManager>
{
    #region Fields

    /// <summary>
    /// 다음 주의 시작 요일을 정의합니다.
    /// </summary>
    private const DayOfWeek resetDayOfWeek = DayOfWeek.Monday;

    /// <summary>
    /// 서버 시간
    /// </summary>
    private long serverUnixTime;

    /// <summary>
    /// 시간 동기화 시점에서의 클라이언트 시간
    /// </summary>
    private double startSinceTime;

    /// <summary>
    /// 캐싱된 날짜
    /// </summary>
    private DateTime cachingDay;

    /// <summary>
    /// 마지막으로 체크한 분과 시간
    /// </summary>
    private int lastCheckedMinute;
    private int lastCheckedHour;

    /// <summary>
    /// 서버 시간과의 최대 허용 차이 (초)
    /// </summary>
    private double limitAllowedTime = 5.0;

    /// <summary>
    /// 마지막 서버 시간 동기화 시간
    /// </summary>
    private DateTime lastSyncTime = DateTime.MinValue;

    /// <summary>
    /// 최소 서버 동기화 간격
    /// </summary>
    private TimeSpan minSyncInterval = TimeSpan.FromMinutes(1);

    #endregion

    #region Properties

    /// <summary>
    /// 서버 시간 utc 기준
    /// </summary>
    public DateTime serverTime => DateTimeOffset.FromUnixTimeSeconds(serverUnixTime).UtcDateTime;

    /// <summary>
    /// 현재 로컬 시간
    /// </summary>
    public DateTime currentLocalTime => currentTime.ToLocalTime();

    /// <summary>
    /// 현재 유닉스 타임스탬프
    /// </summary>
    public long currentUnixTimestamp => ((DateTimeOffset)currentTime).ToUnixTimeSeconds();

    /// <summary>
    /// 현재 틱(Tick)
    /// </summary>
    public long currentTick => currentTime.Ticks;

    /// <summary>
    /// 오늘 날짜 (UTC 기준)
    /// </summary>
    public DateTime today => currentTime.Date;

    /// <summary>
    /// 내일 날짜 (UTC 기준)
    /// </summary>
    public DateTime nextDay => today.AddDays(1).Date;

    /// <summary>
    /// 다음 날까지 남은 시간
    /// </summary>
    public TimeSpan remainTimeToNextDay => nextDay - currentTime;

    /// <summary>
    /// 다음 주까지 남은 시간 utc0시 기준
    /// </summary>
    public TimeSpan remainTimeToNextWeek
    {
        get
        {
            DayOfWeek today = this.today.DayOfWeek;
            int daysToAdd = ((int)resetDayOfWeek - (int)today + 7) % 7;
            if (daysToAdd == 0)
            {
                daysToAdd = 7;
            }
            DateTime nextWeekDay = this.today.AddDays(daysToAdd);
            return nextWeekDay - currentTime;
        }
    }

    /// <summary>
    /// 현재 시간 (UTC 기준)
    /// </summary>
    public DateTime currentTime
    {
        get
        {
            if (serverUnixTime == 0)
            {
                return DateTime.UtcNow;
            }
            DateTime time = serverTime.AddSeconds(Time.realtimeSinceStartupAsDouble - startSinceTime);
            return time;
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// 매 초마다 발생하는 이벤트
    /// </summary>
    public event Action onSecond;

    /// <summary>
    /// 매 분마다 발생하는 이벤트
    /// </summary>
    public event Action onMinute;

    /// <summary>
    /// 매 시간마다 발생하는 이벤트
    /// </summary>
    public event Action onHour;

    /// <summary>
    /// 하루가 지났을 때 발생하는 이벤트
    /// </summary>
    public event Action onNextDay;

    #endregion

    #region Public Methods

    /// <summary>
    /// TimeManager 초기화 (서버 UnixTime 받은 후 최초 1번 수행)
    /// </summary>
    /// <param name="unixTime">서버에서 받은 유닉스 타임스탬프</param>
    public void Initialize(long unixTime)
    {
        serverUnixTime = unixTime;
        startSinceTime = Time.realtimeSinceStartupAsDouble;
        cachingDay = today;
        lastCheckedMinute = currentTime.Minute;
        lastCheckedHour = currentTime.Hour;
        StartTimeProgress();
    }

    /// <summary>
    /// 서버 UnixTime 업데이트
    /// </summary>
    /// <param name="unixTime">서버에서 받은 유닉스 타임스탬프</param>
    public void UpdateServerTick(long unixTime)
    {
        serverUnixTime = unixTime;
        startSinceTime = Time.realtimeSinceStartupAsDouble;
    }

    /// <summary>
    /// 시간 진행 시작
    /// </summary>
    public void StartTimeProgress()
    {
        StopTimeProgress();
        timeProgressCoroutine = StartCoroutine(TimeProgress());
    }

    /// <summary>
    /// 시간 진행 중지
    /// </summary>
    public void StopTimeProgress()
    {
        if (timeProgressCoroutine != null)
        {
            StopCoroutine(timeProgressCoroutine);
            timeProgressCoroutine = null;
        }
    }

    #endregion

    #region Private Methods

    private Coroutine timeProgressCoroutine;

    private IEnumerator TimeProgress()
    {
        double decimalTime = startSinceTime % 1;
        yield return new WaitForSecondsRealtime((float)(1.0 - decimalTime));

        while (true)
        {
            onSecond?.Invoke();

            DateTime now = currentTime;

            if (now.Minute != lastCheckedMinute)
            {
                onMinute?.Invoke();
                lastCheckedMinute = now.Minute;
            }

            if (now.Hour != lastCheckedHour)
            {
                onHour?.Invoke();
                lastCheckedHour = now.Hour;
            }

            if (today > cachingDay)
            {
                onNextDay?.Invoke();
                cachingDay = today;
            }

            VerifyServerTime();

            yield return new WaitForSecondsRealtime(1f);
        }
    }

    private void VerifyServerTime()
    {
        TimeSpan timeDifference = currentTime - DateTime.UtcNow;
        if (Math.Abs(timeDifference.TotalSeconds) > limitAllowedTime)
        {
            Debug.LogWarning("[TimeManager] 서버 시간과의 차이가 너무 큽니다. 시간 동기화를 재요청합니다.");
            RequestServerTimeSync();
        }
    }

    private void RequestServerTimeSync()
    {
        GetCurrentTime();
    }

    public void GetCurrentTime(Action func = null)
    {
        // // 필요에 따라 로딩 팝업, 혹은 게임 일시정지 기능이 필요.
        // // 서버에서 주는 타입에따라 long 혹은 Datetime 등 타입에 따른 처리.
        // var request = new GetTimeRequest { };
        // PlayFabClientAPI.GetTime(request, (result) =>
        // {
        //     func?.Invoke();
        //     return result.time;
        // },
        // (error) =>
        // {
        //     //결과에 따른 에러처리.
        //     return 0;
        // });
    }
    #endregion

    #region Unity Lifecycle

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            // 앱내려갔을때 예외처리. 남은시간에 따른 동기화요청 여부
            if (DateTime.UtcNow - lastSyncTime > minSyncInterval)
            {
                RequestServerTimeSync();
                lastSyncTime = DateTime.UtcNow;
            }
        }
    }
    #endregion
}
