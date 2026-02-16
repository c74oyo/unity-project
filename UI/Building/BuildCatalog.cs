using System.Collections.Generic;
using UnityEngine;

public class BuildCatalog : MonoBehaviour
{
    [Header("1..9 selection order")]
    public List<BuildableDefinition> buildables = new();

    public BuildableDefinition GetByNumberKey()
    {
        // 支持 1~9
        for (int i = 0; i < Mathf.Min(9, buildables.Count); i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                return buildables[i];
        }
        return null;
    }
}
