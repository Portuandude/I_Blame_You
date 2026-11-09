using System.Runtime.CompilerServices;

// EditMode 테스트가 StagePlanner.PickTravel 같은 내부 규칙을 직접 확인할 수 있게 한다.
[assembly: InternalsVisibleTo("IBlameYou.Tests.EditMode")]
