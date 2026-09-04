using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using NUnit.Framework;

namespace Warukyure.Tests.PlayMode
{
    public class UIStateCapture
    {
        const int W = 720;
        const int H = 1280;
        const string OutDir = "design-state/_raw";
        const string GameId = "warukyure";
        const string ScenePath = "Assets/Scenes/Main.unity";

        class RawEntry
        {
            public string id;
            public string path;
            public string role;
            public int x;
            public int y;
            public int w;
            public int h;
            public int z;
            public int? rotation_deg;
            public float[] pivot;
            public string anchor;
            public bool visible;
            public string text;
            public int? font_px;
            public string color;
            public string unityType;
            public string desc;
        }

        class SourceFile
        {
            public string path;
            public string sha256;
        }

        static readonly List<GameObject> s_forcedActive = new List<GameObject>();
        static readonly List<Graphic> s_forcedGraphics = new List<Graphic>();
        static int s_roundedValues;
        static int s_roundedObjects;

        Camera _mainCam;
        RectTransform _canvasRt;
        Canvas _canvas;
        RenderTexture _rt;

        [UnityTest]
        public IEnumerator CaptureTitle()
        {
            LogAssert.ignoreFailingMessages = true;

            try
            {
                Screen.SetResolution(W, H, FullScreenMode.Windowed);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UIStateCapture] Screen.SetResolution failed: {e.Message}");
            }

            AsyncOperation load = SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            Assert.IsNotNull(load, "LoadSceneAsync returned null");
            while (load != null && !load.isDone)
                yield return null;

            // Awake / Start
            yield return null;
            yield return null;
            // UI construction is complete; stop any network coroutines before they complete
            GameObject boardGo = GameObject.Find("Board");
            if (boardGo != null)
            {
                var boardMb = boardGo.GetComponent<MonoBehaviour>();
                if (boardMb != null) boardMb.StopAllCoroutines();
            }
            yield return null;

            _mainCam = Camera.main;
            Assert.IsNotNull(_mainCam, "Main camera not found");

            GameObject canvasGo = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasGo, "Canvas not found");
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvasRt = canvasGo.GetComponent<RectTransform>();
            Assert.IsNotNull(_canvas, "Canvas not found");
            Assert.IsNotNull(_canvasRt, "Canvas RectTransform not found");

            var rtDesc = new RenderTextureDescriptor(W, H, RenderTextureFormat.ARGB32, 24)
            {
                depthStencilFormat = GraphicsFormat.D24_UNorm,
                autoGenerateMips = false,
                useMipMap = false,
                msaaSamples = 1,
            };
            _rt = new RenderTexture(rtDesc);
            if (!_rt.IsCreated()) _rt.Create();
            _mainCam.targetTexture = _rt;

            var prevActive = RenderTexture.active;
            var prevMSAA = _mainCam.allowMSAA;
            var prevHDR = _mainCam.allowHDR;
            var prevRenderMode = _canvas.renderMode;
            var prevWorldCam = _canvas.worldCamera;

            _mainCam.allowMSAA = false;
            _mainCam.allowHDR = false;

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _mainCam;
            _canvas.planeDistance = 1f;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_canvasRt);
            Canvas.ForceUpdateCanvases();

            yield return null;
            yield return null;

            List<RawEntry> entries = new List<RawEntry>();
            Texture2D tex = null;

            try
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_canvasRt);
                Canvas.ForceUpdateCanvases();

                s_forcedActive.Clear();
                s_forcedGraphics.Clear();

                int z = 0;
                Collect(_canvasRt, null, ref z, entries);

                RenderTexture.active = _rt;
                GL.Clear(true, true, _mainCam.backgroundColor);
                _mainCam.Render();

                tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
            }
            finally
            {
                RestoreForcedActive();
            }

            Assert.IsTrue(entries.Count > 0, "No UI objects captured");

            var canvasEntry = entries.Find(e => e.path == "Canvas");
            Assert.IsNotNull(canvasEntry, "Canvas not captured");

            var board = entries.Find(e => e.path == "Canvas/Board");
            Assert.IsNotNull(board, "Board not captured");

            var spin = entries.Find(e => e.path == "Canvas/Spin");
            Assert.IsNotNull(spin, "Spin button not captured");

            string outDir = Path.Combine(GetRepoRoot(), OutDir, "title");
            Directory.CreateDirectory(outDir);

            File.WriteAllBytes(Path.Combine(outDir, "raw.png"), tex.EncodeToPNG());

            string capturedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            string capturedCommit = GetGitCommitShort();
            var sourceFiles = new List<SourceFile>
            {
                new SourceFile { path = ScenePath, sha256 = Sha256File(Path.Combine(GetRepoRoot(), ScenePath)) },
                new SourceFile { path = "Assets/Scripts/WarukyureBoard.cs", sha256 = Sha256File(Path.Combine(GetRepoRoot(), "Assets/Scripts/WarukyureBoard.cs")) },
                new SourceFile { path = "Assets/Scripts/BoardData.cs", sha256 = Sha256File(Path.Combine(GetRepoRoot(), "Assets/Scripts/BoardData.cs")) },
                new SourceFile { path = "Assets/Scripts/SoundMuteButton.cs", sha256 = Sha256File(Path.Combine(GetRepoRoot(), "Assets/Scripts/SoundMuteButton.cs")) },
                new SourceFile { path = "Assets/Scripts/LampAnnouncer.cs", sha256 = Sha256File(Path.Combine(GetRepoRoot(), "Assets/Scripts/LampAnnouncer.cs")) },
            };

            File.WriteAllText(Path.Combine(outDir, "raw.json"),
                ToJson(GameId, "title", ScenePath, Application.unityVersion, capturedAt, capturedCommit, sourceFiles, entries),
                new UTF8Encoding(false));

            _mainCam.allowMSAA = prevMSAA;
            _mainCam.allowHDR = prevHDR;
            _mainCam.targetTexture = null;

            _canvas.renderMode = prevRenderMode;
            _canvas.worldCamera = prevWorldCam;
            Canvas.ForceUpdateCanvases();

            RenderTexture.active = prevActive;
            if (_rt != null) UnityEngine.Object.Destroy(_rt);

            Debug.Log($"[UIStateCapture] title: {entries.Count} objects, roundedValues={s_roundedValues}, roundedObjects={s_roundedObjects}");

            Assert.Pass("UI state captured for title");
        }

        void ForceActivateForMeasure(Transform t)
        {
            var go = t.gameObject;
            bool wasInactive = !go.activeSelf;
            if (wasInactive)
            {
                s_forcedActive.Add(go);
                go.SetActive(true);
                var graphics = go.GetComponents<Graphic>();
                for (int i = 0; i < graphics.Length; i++)
                {
                    var g = graphics[i];
                    if (g != null && g.enabled)
                    {
                        s_forcedGraphics.Add(g);
                        g.enabled = true;
                    }
                }
            }
            for (int i = 0; i < t.childCount; i++)
                ForceActivateForMeasure(t.GetChild(i));
        }

        void RestoreForcedActive()
        {
            foreach (var g in s_forcedGraphics)
            {
                if (g != null)
                    g.enabled = true;
            }
            s_forcedGraphics.Clear();
            foreach (var go in s_forcedActive)
            {
                if (go != null)
                    go.SetActive(false);
            }
            s_forcedActive.Clear();
        }

        bool IsMeasuredWhileInactive(Transform t)
        {
            for (; t != null; t = t.parent)
            {
                if (s_forcedActive.Contains(t.gameObject))
                    return true;
            }
            return false;
        }

        void Collect(RectTransform rt, string parentPath, ref int z, List<RawEntry> entries)
        {
            string path = parentPath == null ? rt.name : parentPath + "/" + rt.name;

            if (ShouldSkip(rt, path))
                return;

            var entry = BuildEntry(rt, path, z++);
            if (entry != null)
                entries.Add(entry);

            for (int i = 0; i < rt.childCount; i++)
            {
                var child = rt.GetChild(i) as RectTransform;
                if (child != null)
                    Collect(child, path, ref z, entries);
            }
        }

        bool ShouldSkip(RectTransform rt, string path)
        {
            if (rt.GetComponent<TMP_SubMeshUI>() != null)
                return true;
            return false;
        }

        RawEntry BuildEntry(RectTransform rt, string path, int z)
        {
            bool rounded;
            var (x, y, w, h, hasRounding) = GetScreenRect(rt, out rounded);
            if (hasRounding)
            {
                s_roundedValues += 4;
                s_roundedObjects++;
            }

            if (w <= 0 || h <= 0)
                return null;

            bool visible = ComputeVisible(rt, x, y, w, h);
            if (!visible)
                return null;

            var props = GetProperties(rt);

            int? rotation = null;
            float delta = Mathf.DeltaAngle(0f, rt.eulerAngles.z);
            if (Mathf.Abs(delta) > 0.5f)
                rotation = Mathf.RoundToInt(delta);

            string id = null;
            if (path == "Canvas/Spin")
                id = "spinbutton";
            else if (path == "Canvas/Help")
                id = "helpbutton";
            else if (path == "Canvas/Board")
                id = "board";
            else if (path == "Canvas/WalletText")
                id = "wallettext";
            else if (path == "Canvas/SoundMuteButton")
                id = "soundmutebutton";
            else if (path.StartsWith("Canvas/Bet") && !path.Contains("/"))
            {
                var tail = path.Substring("Canvas/Bet".Length);
                id = "betbutton_" + tail;
            }

            string desc = null;
            if (path == "Canvas")
                desc = "Canvas root";
            else if (path == "Canvas/Board")
                desc = "ボード（盤面）";
            else if (path == "Canvas/WalletTextBand")
                desc = "残高帯";
            else if (path == "Canvas/WalletText")
                desc = "残高テキスト";
            else if (path == "Canvas/AdVirtua")
                desc = "Ad-Virtua領域";
            else if (path == "Canvas/Spin")
                desc = "SPINボタン";
            else if (path == "Canvas/Help")
                desc = "ヘルプボタン";
            else if (path == "Canvas/SoundMuteButton")
                desc = "サウンドミュートボタン";
            else if (path.StartsWith("Canvas/Bet") && !path.Contains("/"))
                desc = "BETボタン";
            else if (path.StartsWith("Canvas/dim_"))
                desc = "消灯マス";
            else if (path.StartsWith("Canvas/CollectionBall"))
                desc = "コレクションボール";

            return new RawEntry
            {
                id = id,
                path = path,
                role = props.role ?? GetRole(props.unityType),
                x = x,
                y = y,
                w = w,
                h = h,
                z = z,
                rotation_deg = rotation,
                pivot = new[] { rt.pivot.x, rt.pivot.y },
                anchor = ClassifyAnchor(rt.anchorMin, rt.anchorMax),
                visible = true,
                text = props.text,
                font_px = props.fontPx,
                color = props.color,
                unityType = props.unityType,
                desc = desc,
            };
        }

        (int x, int y, int w, int h, bool rounded) GetScreenRect(RectTransform rt, out bool rounded)
        {
            rounded = false;
            Canvas canvas = FindCanvasFor(rt);
            Camera cam = canvas != null ? canvas.worldCamera : null;
            if (cam == null)
                cam = _mainCam;
            if (cam == null)
                throw new InvalidOperationException($"no camera for {rt.name}");

            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                if (sp.x < minX) minX = sp.x;
                if (sp.x > maxX) maxX = sp.x;
                if (sp.y < minY) minY = sp.y;
                if (sp.y > maxY) maxY = sp.y;
            }

            float actualW = W, actualH = H;
            if (cam.targetTexture != null)
            {
                actualW = cam.targetTexture.width;
                actualH = cam.targetTexture.height;
            }
            float scale = (float)W / actualW;

            int minXi = RoundScale(minX, scale, out bool r1);
            int maxXi = RoundScale(maxX, scale, out bool r2);
            int minYi = RoundScale(minY, scale, out bool r3);
            int maxYi = RoundScale(maxY, scale, out bool r4);
            rounded = r1 || r2 || r3 || r4;

            int x = minXi;
            int y = H - maxYi;
            int w = maxXi - minXi;
            int h = maxYi - minYi;
            return (x, y, w, h, rounded);
        }

        static int RoundScale(float v, float scale, out bool rounded)
        {
            float raw = v * scale;
            int result = Mathf.FloorToInt(raw + 0.5f);
            rounded = Mathf.Abs(raw - result) > 0.001f;
            return result;
        }

        static Canvas FindCanvasFor(RectTransform rt)
        {
            var c = rt.GetComponent<Canvas>();
            if (c != null) return c;
            return rt.GetComponentInParent<Canvas>();
        }

        static string ClassifyAnchor(Vector2 min, Vector2 max)
        {
            const float eps = 0.001f;

            string horizontal;
            if (Mathf.Abs(min.x) < eps && Mathf.Abs(max.x) < eps)
                horizontal = "left";
            else if (Mathf.Abs(min.x - 0.5f) < eps && Mathf.Abs(max.x - 0.5f) < eps)
                horizontal = "center";
            else if (Mathf.Abs(min.x - 1f) < eps && Mathf.Abs(max.x - 1f) < eps)
                horizontal = "right";
            else if (Mathf.Abs(min.x) < eps && Mathf.Abs(max.x - 1f) < eps)
                horizontal = "stretch";
            else
                return "custom";

            string vertical;
            if (Mathf.Abs(min.y) < eps && Mathf.Abs(max.y) < eps)
                vertical = "bottom";
            else if (Mathf.Abs(min.y - 0.5f) < eps && Mathf.Abs(max.y - 0.5f) < eps)
                vertical = "middle";
            else if (Mathf.Abs(min.y - 1f) < eps && Mathf.Abs(max.y - 1f) < eps)
                vertical = "top";
            else if (Mathf.Abs(min.y) < eps && Mathf.Abs(max.y - 1f) < eps)
                vertical = "stretch";
            else
                return "custom";

            return $"{vertical}-{horizontal}";
        }

        bool ComputeVisible(RectTransform rt, int x, int y, int w, int h)
        {
            if (!rt.gameObject.activeInHierarchy)
                return false;
            if (w <= 0 || h <= 0)
                return false;
            if (x >= W || y >= H || x + w <= 0 || y + h <= 0)
                return false;

            if (rt.GetComponent<Button>() != null)
                return true;

            float alpha = 1f;
            for (Transform t = rt; t != null; t = t.parent)
            {
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null)
                    alpha *= cg.alpha;
            }
            if (alpha <= 0.0001f)
                return false;

            var graphics = rt.GetComponents<Graphic>();
            if (graphics.Length > 0)
            {
                float maxA = 0f;
                for (int i = 0; i < graphics.Length; i++)
                {
                    var g = graphics[i];
                    if (g != null && g.enabled && g.color.a > maxA)
                        maxA = g.color.a;
                }
                if (maxA <= 0.0001f)
                    return false;
            }

            return true;
        }

        (string text, int? fontPx, string color, string unityType, string role) GetProperties(RectTransform rt)
        {
            string unityType = GetUnityType(rt);
            string text = null;
            int? fontPx = null;
            string color = null;
            string role = null;

            var graphics = rt.GetComponents<Graphic>();
            Graphic first = null;
            for (int i = 0; i < graphics.Length; i++)
            {
                var g = graphics[i];
                if (g != null && g.enabled) { first = g; break; }
            }

            if (first != null)
            {
                color = ColorToHex(first.color);

                if (first is TMP_Text tmp)
                {
                    if (rt.gameObject.activeInHierarchy)
                        tmp.ForceMeshUpdate();
                    text = tmp.text;
                    fontPx = Mathf.FloorToInt(tmp.fontSize + 0.5f);
                }
                else if (first is Text legacy)
                {
                    text = legacy.text;
                    if (legacy.fontSize > 0)
                        fontPx = GetFontPx(legacy.fontSize, rt);
                }
                else if (first is RawImage)
                {
                    role = "image";
                }
                else if (first is Image && rt.GetComponent<Button>() != null)
                {
                    role = "button";
                }
            }

            return (text, fontPx, color, unityType, role);
        }

        int GetFontPx(float fontSize, RectTransform rt)
        {
            Canvas canvas = FindCanvasFor(rt);
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float raw = fontSize * scale;
            return Mathf.FloorToInt(raw + 0.5f);
        }

        static string GetUnityType(RectTransform rt)
        {
            if (rt.GetComponent<Button>() != null) return "Button";
            if (rt.GetComponent<TMP_Text>() != null) return "TextMeshProUGUI";
            if (rt.GetComponent<Text>() != null) return "Text";
            if (rt.GetComponent<RawImage>() != null) return "RawImage";
            if (rt.GetComponent<Image>() != null) return "Image";
            if (rt.GetComponent<Canvas>() != null) return "Canvas";
            return null;
        }

        static string ColorToHex(Color c)
        {
            int r = Mathf.FloorToInt(c.r * 255 + 0.5f);
            int g = Mathf.FloorToInt(c.g * 255 + 0.5f);
            int b = Mathf.FloorToInt(c.b * 255 + 0.5f);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        static string GetRole(string unityType)
        {
            if (string.IsNullOrEmpty(unityType))
                return "panel";
            switch (unityType.ToLowerInvariant())
            {
                case "button": return "button";
                case "image":
                case "rawimage": return "image";
                case "text":
                case "textmeshprougui": return "text";
                case "canvas": return "panel";
                default: return "panel";
            }
        }

        static string ToJson(string gameId, string screenId, string scenePath, string unityVersion,
            string capturedAt, string capturedCommit, List<SourceFile> sourceFiles, List<RawEntry> entries)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append($"\"game_id\":{JsonString(gameId)},");
            sb.Append($"\"screen_id\":{JsonString(screenId)},");
            sb.Append($"\"scene_path\":{JsonString(scenePath)},");
            sb.Append($"\"unity_version\":{JsonString(unityVersion)},");
            sb.Append($"\"captured_at\":{JsonString(capturedAt)},");
            sb.Append($"\"captured_commit\":{JsonString(capturedCommit)},");
            sb.Append("\"source_files\":[");
            for (int i = 0; i < sourceFiles.Count; i++)
            {
                var sf = sourceFiles[i];
                sb.Append("{");
                sb.Append($"\"path\":{JsonString(sf.path)},");
                sb.Append($"\"sha256\":{JsonString(sf.sha256)}");
                sb.Append("}");
                if (i < sourceFiles.Count - 1)
                    sb.Append(",");
            }
            sb.Append("],");
            sb.AppendLine("\"objects\":[");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                sb.Append("  {");
                sb.Append($"\"path\":{JsonString(e.path)},");
                if (!string.IsNullOrEmpty(e.id))
                    sb.Append($"\"id\":{JsonString(e.id)},");
                sb.Append($"\"role\":{JsonString(e.role)},");
                sb.Append($"\"x\":{e.x},");
                sb.Append($"\"y\":{e.y},");
                sb.Append($"\"w\":{e.w},");
                sb.Append($"\"h\":{e.h},");
                sb.Append($"\"z\":{e.z},");
                if (e.rotation_deg.HasValue)
                    sb.Append($"\"rotation_deg\":{e.rotation_deg.Value},");
                sb.Append($"\"pivot\":[{e.pivot[0].ToString("R")},{e.pivot[1].ToString("R")}],");
                sb.Append($"\"anchor\":{JsonString(e.anchor)},");
                sb.Append($"\"visible\":{(e.visible ? "true" : "false")},");
                sb.Append($"\"text\":{JsonString(e.text)},");
                if (e.font_px.HasValue)
                    sb.Append($"\"font_px\":{e.font_px.Value},");
                else
                    sb.Append("\"font_px\":null,");
                sb.Append($"\"color\":{JsonString(e.color)},");
                sb.Append($"\"unityType\":{JsonString(e.unityType)}");
                if (!string.IsNullOrEmpty(e.desc))
                    sb.Append($",\"desc\":{JsonString(e.desc)}");
                sb.Append("}");
                if (i < entries.Count - 1)
                    sb.Append(",");
                sb.AppendLine();
            }
            sb.Append("  ]");
            sb.Append("}");
            return sb.ToString();
        }

        static string JsonString(string s)
        {
            if (s == null)
                return "null";
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append($"\\u{(int)c:X4}");
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        static string GetRepoRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        static string GetGitCommitShort()
        {
            string repoRoot = GetRepoRoot();

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git")
                {
                    WorkingDirectory = repoRoot,
                    Arguments = "rev-parse --short HEAD",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                var p = System.Diagnostics.Process.Start(psi);
                if (p != null)
                {
                    using (p)
                    {
                        p.WaitForExit();
                        if (p.ExitCode == 0)
                        {
                            string output = p.StandardOutput.ReadToEnd().Trim();
                            if (!string.IsNullOrEmpty(output))
                                return output;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UIStateCapture] git rev-parse failed: {ex.Message}");
            }

            return "0000000";
        }

        static string Sha256File(string fullPath)
        {
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"source file not found: {fullPath}");

            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(fullPath))
            {
                var hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
