using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Cards/Player Worker Roster", fileName = "PlayerWorkerRoster")]
public class PlayerWorkerRoster : ScriptableObject
{
    public List<CharacterCard> ownedCards = new();
}
