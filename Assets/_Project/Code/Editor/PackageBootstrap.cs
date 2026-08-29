using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Fuegt Pakete headless hinzu. Aufruf:
    /// Unity -batchmode -quit -executeMethod Infront.EditorTools.PackageBootstrap.AddNetcode
    /// Unity waehlt die zur Editor-Version passende Paketversion selbst.
    /// </summary>
    public static class PackageBootstrap
    {
        public static void AddNetcode()
        {
            AddPackage("com.unity.netcode.gameobjects");
        }

        static void AddPackage(string name)
        {
            Debug.Log($"PKG_ADD_START: {name}");
            AddRequest request = Client.Add(name);
            while (!request.IsCompleted)
                System.Threading.Thread.Sleep(100);

            if (request.Status == StatusCode.Success)
                Debug.Log($"PKG_ADD_OK: {request.Result.packageId}");
            else
                Debug.LogError($"PKG_ADD_FAIL: {name} -> {request.Error?.message}");
        }
    }
}
