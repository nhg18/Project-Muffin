using NUnit.Framework;

namespace Chapchu.Game.Tests
{
    /// <summary>
    /// GameServer EditMode 테스트. Photon 없이 GameServer 만 만들어 규칙을 확인한다.
    /// </summary>
    public class GameServerTests
    {
        // 테스트 어셈블리가 Muffin.Game.Server 를 참조하는지만 확인한다. 규칙 테스트는 1201 · 1202 에서 추가.
        [Test]
        public void NewServer_HasNoTurnActor()
        {
            var server = new GameServer(null);

            Assert.AreEqual(-1, server.CurrentTurnActor);
        }
    }
}
