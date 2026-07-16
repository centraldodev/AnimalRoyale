using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace AnimalBattleRoyale
{
    public sealed class TemporaryMultiplayerSmokeDriver : MonoBehaviour
    {
        private bool host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool enabled = Array.Exists(args, arg => arg == "-abr-smoke-host" || arg == "-abr-smoke-client");
            if (!enabled) return;
            new GameObject("TemporaryMultiplayerSmokeDriver").AddComponent<TemporaryMultiplayerSmokeDriver>();
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            host = Array.Exists(args, arg => arg == "-abr-smoke-host");
            while (OnlineMultiplayerManager.Instance == null) yield return null;
            yield return new WaitForSecondsRealtime(1f);

            InvokePrivate(host ? "StartLocalHost" : "StartLocalClient");
            Debug.Log($"[ONLINE_SMOKE] {(host ? "Host" : "Client")} requested.");

            if (host)
            {
                yield return new WaitForSecondsRealtime(7f);
                InvokePrivate("StartHostedMatch");
                Debug.Log("[ONLINE_SMOKE] Host requested match start.");
            }

            yield return new WaitForSecondsRealtime(22f);
            OnlineMultiplayerManager online = OnlineMultiplayerManager.Instance;
            int fighters = BattleRoyaleManager.Instance != null ? BattleRoyaleManager.Instance.Fighters.Count : 0;
            bool success = online != null && online.MatchStarted && fighters == 10;
            Debug.Log($"[ONLINE_SMOKE] RESULT role={(host ? "host" : "client")} connected={online != null && online.IsConnected} "
                      + $"started={online != null && online.MatchStarted} fighters={fighters} success={success}");
            Application.Quit(success ? 0 : 3);
        }

        private static void InvokePrivate(string methodName)
        {
            MethodInfo method = typeof(OnlineMultiplayerManager).GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(OnlineMultiplayerManager.Instance, null);
        }
    }
}
