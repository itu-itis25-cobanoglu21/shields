using UnityEngine;

namespace Shields.Players
{
    /// <summary>
    /// Marks a PlayerCharacter as bot controlled. It makes no decisions and holds no AI.
    /// Its only job today is to occupy a seat and give future bot logic a home, so the
    /// rest of the code never has to ask whether a seat is human by elimination.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    public class BotController : MonoBehaviour
    {
        PlayerCharacter _character;

        public PlayerCharacter Character
        {
            get
            {
                if (_character == null) _character = GetComponent<PlayerCharacter>();
                return _character;
            }
        }

        /// <summary>True once the bootstrap has handed this seat to bot control.</summary>
        public bool IsActive { get { return enabled; } }
    }
}
