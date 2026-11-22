using System.Collections.Generic;
using System.Linq;
using IBlameYou.Net;
using NUnit.Framework;
using UnityEngine;

namespace IBlameYou.Tests
{
    // 온라인 2인: 호스트가 보낸 그림 조각·HUD 명령을 친구 쪽이 같은 모양으로 되살리는지 (Unity 안에서, CI editmode).
    // 같은 씬에서 보내고 받으므로 받은 조각은 NetWorldCapture.Ignore 아래에 둔다.
    public class NetViewTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private readonly List<Object> createdAssets = new List<Object>();
        private List<byte[]> outbox;
        private NetAssetSender sender;
        private NetAssetReceiver receiver;

        [SetUp]
        public void SetUp()
        {
            outbox = new List<byte[]>();
            sender = new NetAssetSender(outbox.Add);
            receiver = new NetAssetReceiver();
        }

        [TearDown]
        public void TearDown()
        {
            HudRecorder.Recording = false;
            HudRecorder.ForceRepaintForTests = false;
            HudRecorder.Assets = null;
            HudRecorder.ResetTables();
            receiver.Clear();
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            foreach (var a in createdAssets) if (a != null) Object.DestroyImmediate(a);
            created.Clear();
            createdAssets.Clear();
        }

        private GameObject Make(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }

        private Texture2D MakeTexture(Color color)
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = Enumerable.Repeat((Color32)color, 16).ToArray();
            t.SetPixels32(pixels);
            t.Apply();
            createdAssets.Add(t);
            return t;
        }

        // 보낸 메시지를 친구 쪽 처리 순서대로 풀어 준다 (Texture/Sprite는 표에, World/Hud는 view에)
        private void Deliver(byte[] message, NetWorldView world, HudReplay hud)
        {
            var data = NetCompression.Unwrap(NetCompression.MaybeCompress(message)); // 압축도 같이 거친다
            var r = new NetReader(data, 1, data.Length - 1);
            switch ((MsgType)data[0])
            {
                case MsgType.Texture: receiver.ReadTexture(r); break;
                case MsgType.Sprite: receiver.ReadSprite(r); break;
                case MsgType.World: world?.Read(r); break;
                case MsgType.Hud: hud?.Read(r); break;
                default: Assert.Fail("모르는 메시지 " + data[0]); break;
            }
            Assert.IsTrue(r.AtEnd, "메시지를 끝까지 읽어야 한다: " + (MsgType)data[0]);
        }

        private void DeliverAll(NetWorldView world, HudReplay hud)
        {
            foreach (var m in outbox) Deliver(m, world, hud);
            outbox.Clear();
        }

        [Test]
        public void World_SpriteLineAndTextArriveWithSameLook_AndRemovalsFollow()
        {
            var texture = MakeTexture(Color.red);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0f), 16f);
            createdAssets.Add(sprite);

            var hero = Make("Hero");
            hero.transform.position = new Vector3(3f, -2f, 0f);
            hero.transform.localScale = new Vector3(-2f, 1.5f, 1f);
            var sr = hero.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 1f, 1f, 0.5f);
            sr.flipX = true;
            sr.sortingOrder = 7;

            var lineGo = Make("Line");
            var line = lineGo.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 3;
            line.SetPositions(new[] { Vector3.zero, Vector3.right, new Vector3(1f, 1f, 0f) });
            line.startWidth = 0.2f;
            line.endWidth = 0.05f;
            line.startColor = Color.green;
            line.endColor = Color.blue;

            var textGo = Make("Text");
            var text = textGo.AddComponent<TextMesh>();
            text.text = "+12";
            text.fontSize = 40;
            text.color = Color.yellow;

            var viewRoot = Make("View");
            var camGo = Make("Cam");
            var cam = camGo.AddComponent<Camera>();
            var world = new NetWorldView(receiver, viewRoot.transform, cam);
            var capture = new NetWorldCapture(sender) { Ignore = viewRoot.transform };

            outbox.Add(capture.Capture());
            DeliverAll(world, null);
            Assert.GreaterOrEqual(world.PieceCount, 3);

            var mirroredHero = viewRoot.GetComponentsInChildren<SpriteRenderer>().Single(r => r.sprite != null && r.sprite.texture.width == 4);
            Assert.AreEqual(new Vector3(3f, -2f, 0f), mirroredHero.transform.position);
            Assert.AreEqual(-2f, mirroredHero.transform.localScale.x, 1e-4f);
            Assert.IsTrue(mirroredHero.flipX);
            Assert.AreEqual(7, mirroredHero.sortingOrder);
            Assert.AreEqual(0.5f, mirroredHero.color.a, 0.01f);
            Assert.AreEqual(16f, mirroredHero.sprite.pixelsPerUnit, 1e-4f);
            Assert.AreEqual(Color.red, (Color)mirroredHero.sprite.texture.GetPixel(1, 1));

            var mirroredLine = viewRoot.GetComponentInChildren<LineRenderer>();
            Assert.AreEqual(3, mirroredLine.positionCount);
            Assert.AreEqual(new Vector3(1f, 1f, 0f), mirroredLine.GetPosition(2));
            Assert.AreEqual(0.2f, mirroredLine.startWidth, 1e-3f);

            var mirroredText = viewRoot.GetComponentInChildren<TextMesh>();
            Assert.AreEqual("+12", mirroredText.text);
            Assert.AreEqual(40, mirroredText.fontSize);

            // 바뀐 것이 없으면 다음 메시지에는 조각이 없다 (카메라만)
            var quiet = capture.Capture();
            outbox.Add(quiet);
            Assert.Less(quiet.Length, 64);
            DeliverAll(world, null);

            // 옮기고 하나를 없애면: 옮긴 것만 보내고 없앤 것은 지운다
            hero.transform.position = new Vector3(5f, 0f, 0f);
            Object.DestroyImmediate(textGo);
            outbox.Add(capture.Capture());
            DeliverAll(world, null);
            Assert.AreEqual(new Vector3(5f, 0f, 0f), mirroredHero.transform.position);
            Assert.IsNull(viewRoot.GetComponentInChildren<TextMesh>());

            // 같은 스프라이트는 한 번만 보낸다
            sr.color = Color.white;
            capture.Capture();
            Assert.IsFalse(outbox.Any(m => m[0] == (byte)MsgType.Sprite || m[0] == (byte)MsgType.Texture));
        }

        [Test]
        public void World_CameraFollowsHost()
        {
            var hostCamGo = Make("HostCam");
            hostCamGo.tag = "MainCamera";
            var hostCam = hostCamGo.AddComponent<Camera>();
            hostCam.orthographicSize = 7.5f;
            hostCam.backgroundColor = Color.magenta;
            hostCamGo.transform.position = new Vector3(10f, 4f, -10f);

            var viewRoot = Make("View");
            var camGo = Make("ViewCam");
            var cam = camGo.AddComponent<Camera>();
            var world = new NetWorldView(receiver, viewRoot.transform, cam);
            var capture = new NetWorldCapture(sender) { Ignore = viewRoot.transform };
            var message = capture.Capture();
            Deliver(message, world, null);

            if (Camera.main != hostCam) Assert.Ignore("이 환경에서는 Camera.main이 테스트 카메라가 아니다");
            Assert.AreEqual(new Vector3(10f, 4f, -10f), cam.transform.position);
            Assert.AreEqual(7.5f, cam.orthographicSize, 1e-4f);
            Assert.AreEqual((Color32)Color.magenta, (Color32)cam.backgroundColor);
        }

        [Test]
        public void Hud_RecordedCommandsAndStylesReplayInOrder()
        {
            var background = MakeTexture(Color.blue);
            var style = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
            style.normal.textColor = Color.red;
            style.normal.background = background;
            style.padding = new RectOffset(1, 2, 3, 4);

            HudRecorder.ResetTables();
            HudRecorder.Assets = sender;
            HudRecorder.Recording = true;
            HudRecorder.ForceRepaintForTests = true;
            try
            {
                HudRecorder.Content(HudOp.Label, new Rect(10, 20, 300, 40), new GUIContent("체력 100"), style, 0);
                HudRecorder.Group(new Rect(5, 5, 50, 50));
                HudRecorder.Texture(new Rect(0, 0, 8, 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, false, Color.white, Vector4.zero, Vector4.zero);
                HudRecorder.EndGroup();
                style.normal.textColor = Color.green; // 모양이 바뀌면 새 번호로 정의 (번호는 객체가 아니라 모양에 붙는다)
                HudRecorder.Content(HudOp.Box, new Rect(0, 0, 10, 10), new GUIContent("마나"), style, 0);
            }
            catch (System.Exception ex) when (ex.Message.Contains("OnGUI"))
            {
                Assert.Ignore("OnGUI 밖이라 GUI 상태를 읽을 수 없는 환경: " + ex.Message);
            }
            finally
            {
                HudRecorder.ForceRepaintForTests = false;
            }

            var frames = new List<byte[]>();
            HudRecorder.TakeCompleted(frames, true);
            Assert.AreEqual(1, frames.Count);
            outbox.AddRange(frames);

            var hud = new HudReplay(receiver);
            DeliverAll(null, hud);
            Assert.AreEqual(Mathf.Max(1, Screen.width), hud.HostWidth);
            Assert.AreEqual("체력 100", hud.TextAt(0));
            Assert.AreEqual("마나", hud.TextAt(1));
            Assert.Greater(hud.CommandCount, 5); // 상태(행렬·색·켜짐) + 글자 + 묶음 + 그림 + 묶음 끝 + 상자

            Assert.AreEqual((Color32)Color.red, (Color32)hud.StyleById(1).normal.textColor); // 처음 모양
            var replayed = hud.StyleById(2);
            Assert.IsNotNull(replayed);
            Assert.AreEqual(22, replayed.fontSize);
            Assert.AreEqual(FontStyle.Bold, replayed.fontStyle);
            Assert.AreEqual(TextAnchor.MiddleCenter, replayed.alignment);
            Assert.IsTrue(replayed.richText);
            Assert.AreEqual(3, replayed.padding.top);
            Assert.AreEqual((Color32)Color.green, (Color32)replayed.normal.textColor); // 바뀐 모양
            Assert.IsNotNull(replayed.normal.background);
            Assert.AreEqual(Color.blue, replayed.normal.background.GetPixel(0, 0));
        }

        // OnGUI마다 new GUIStyle을 만드는 HUD가 많아서, 같은 모양은 객체가 달라도 번호 하나로 묶어야 표가 끝없이 커지지 않는다 (버그 점검 2026-11-22).
        [Test]
        public void Hud_SameLookingStylesShareOneId()
        {
            HudRecorder.ResetTables();
            HudRecorder.Assets = sender;
            HudRecorder.Recording = true;
            HudRecorder.ForceRepaintForTests = true;
            try
            {
                for (int i = 0; i < 50; i++)
                {
                    var style = new GUIStyle { fontSize = 18, richText = true }; // 프레임마다 새 객체 (CuteButton 등과 같은 방식)
                    HudRecorder.Content(HudOp.Label, new Rect(0, i, 10, 10), new GUIContent("x"), style, 0);
                }
            }
            catch (System.Exception ex) when (ex.Message.Contains("OnGUI"))
            {
                Assert.Ignore("OnGUI 밖이라 GUI 상태를 읽을 수 없는 환경: " + ex.Message);
            }
            finally
            {
                HudRecorder.ForceRepaintForTests = false;
            }

            var frames = new List<byte[]>();
            HudRecorder.TakeCompleted(frames, true);
            outbox.AddRange(frames);
            var hud = new HudReplay(receiver);
            DeliverAll(null, hud);
            Assert.IsNotNull(hud.StyleById(1));
            Assert.AreEqual(18, hud.StyleById(1).fontSize);
            Assert.IsNull(hud.StyleById(2)); // 같은 모양 49개는 새 번호를 받지 않았다
        }

        [Test]
        public void Hud_NothingIsRecordedWhenNotHosting()
        {
            HudRecorder.ResetTables();
            HudRecorder.Assets = sender;
            HudRecorder.Recording = false;
            HudRecorder.ForceRepaintForTests = true;
            try
            {
                HudRecorder.Content(HudOp.Label, new Rect(0, 0, 1, 1), new GUIContent("x"), GUIStyle.none, 0);
            }
            finally
            {
                HudRecorder.ForceRepaintForTests = false;
            }
            var frames = new List<byte[]>();
            HudRecorder.TakeCompleted(frames, true);
            Assert.AreEqual(0, frames.Count);
            Assert.AreEqual(0, outbox.Count);
        }

        [Test]
        public void Assets_WhiteTextureIsNeverSent()
        {
            Assert.AreEqual(NetAssetSender.WhiteTexture, sender.TextureId(Texture2D.whiteTexture));
            Assert.AreEqual(NetAssetSender.NoTexture, sender.TextureId(null));
            Assert.AreEqual(0, outbox.Count);
            Assert.AreSame(Texture2D.whiteTexture, receiver.Texture(NetAssetSender.WhiteTexture));
        }
    }
}
