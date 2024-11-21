using UnityEngine;

public class TimeManagerExample : MonoBehaviour
{
    private void Start()
    {
        // 서버로부터 현재 유닉스 타임스탬프를 받아왔다고 가정
        long serverUnixTime = TimeManager.Instance.GetCurrentTime();

        // TimeManager 초기화
        TimeManager.Instance.Initialize(serverUnixTime);

        // 시간 이벤트에 콜백 함수 등록
        TimeManager.Instance.onSecond += OnEverySecond;
        TimeManager.Instance.onMinute += OnEveryMinute;
        TimeManager.Instance.onHour += OnEveryHour;
        TimeManager.Instance.onNextDay += OnNextDay;
    }
    // 매 초마다 호출되는 콜백 함수
    private void OnEverySecond()
    {
        //초 단위 이벤트
    }

    // 매 분마다 호출되는 콜백 함수
    private void OnEveryMinute()
    {
        //분 단위 이벤트
    }

    // 매 시간마다 호출되는 콜백 함수
    private void OnEveryHour()
    {
        //시간 단위 이벤트
    }

    // 하루가 지났을 때 호출되는 콜백 함수
    private void OnNextDay()
    {
        //일 단위 이벤트
    }

    private void ResetEvent()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.onSecond -= OnEverySecond;
            TimeManager.Instance.onMinute -= OnEveryMinute;
            TimeManager.Instance.onHour -= OnEveryHour;
            TimeManager.Instance.onNextDay -= OnNextDay;
        }
    }
}
