// 인스펙터에서 수치를 고치기 위한 ScriptableObject. 값은 Core/StairsConfig(웹 CONFIG와 같은 기본값)에 있다.
using Stairs.Core;
using UnityEngine;

namespace Stairs.Game
{
    [CreateAssetMenu(menuName = "Stairs/Game Config")]
    public class GameConfigAsset : ScriptableObject
    {
        public StairsConfig config = new StairsConfig();
    }
}
