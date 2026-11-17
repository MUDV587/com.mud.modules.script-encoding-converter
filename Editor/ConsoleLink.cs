// Copyright (c) Bian Shanghai
// https://github.com/Bian-Sh/Script-Encoding-Converter
// Licensed under the MIT license. See the LICENSE file in the package root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MUD.Modules.ScriptEncodingConverter
{
    /// <summary> Makes the file paths printed in Console logs clickable (Ping/Highlight in Project window). </summary>
    internal static class ConsoleLink
    {
        [InitializeOnLoadMethod]
        static void Init()
        {
#if UNITY_2021_1_OR_NEWER
            EditorGUI.hyperLinkClicked += OnLinkClicked;
            static void OnLinkClicked(EditorWindow ew, HyperLinkClickedEventArgs args)
            {
                if (args.hyperLinkData.TryGetValue("sourcefile", out var file))
                {
                    PingScriptAsset(file);
                }
            }
#else
            // Legacy path for Unity < 2021.1: hook the private hyperLinkClicked event via reflection.
            // Never let a reflection failure spam the Console on every domain reload.
            try
            {
                var evt = typeof(EditorGUI).GetEvent("hyperLinkClicked", BindingFlags.Static | BindingFlags.NonPublic);
                if (evt != null)
                {
                    var handler = Delegate.CreateDelegate(evt.EventHandlerType, typeof(ConsoleLink), nameof(OnLinkClicked));
                    evt.AddMethod.Invoke(null, new object[] { handler });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Script Encoding Converter: could not hook Console hyperlinks on this Unity version ({e.Message}). Clickable file paths will not work.");
            }
        }

        static void OnLinkClicked(object sender, EventArgs args)
        {
            var property = args.GetType().GetProperty("hyperlinkInfos", BindingFlags.Instance | BindingFlags.Public);
            if (property?.GetValue(args) is Dictionary<string, string> infos && infos.TryGetValue("sourcefile", out var file))
            {
                PingScriptAsset(file);
            }
#endif
        }

        // .cs assets are MonoScript, not TextAsset — always load via UnityEngine.Object so the Ping works for any asset type.
        static void PingScriptAsset(string file)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }
    }
}
