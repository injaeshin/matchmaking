# matchmaking
C# - Redis 를 이용한 매칭시스템

## TODO

[Task]
매칭의 평균 시간에 따라 Task 증가, 감소
Task 증가, 감소 쿨타임

큐가 비었을 경우 대기(5초)
5초 대기 중 요청이 들어왔을 경우 활성화

큐의 대기 인원이 적을 경우 Task 1개만 활성화

[Lock]
로컬 락, 레디스 락 동시 사용 (스케일 아웃용)

[Pool]
MatchQueueItem 메모리 풀 활용
