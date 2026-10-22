using System.Collections.Generic;
using Gstarr.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.World
{
    /// <summary>
    /// Doors 타일맵의 칸을 문으로 만든다.
    /// 같은 x의 칸이 문 하나고, 이웃한 두 문 사이가 방이다.
    /// 방에 들어가면 양쪽 문이 잠기고, 그 안의 적을 모두 처치하면 열린다.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    [DefaultExecutionOrder(100)]
    public class ArenaDoorSpawner : MonoBehaviour
    {
        private readonly List<ArenaRoom> rooms = new List<ArenaRoom>();
        private Collider2D playerBody;

        private void Start()
        {
            var tilemap = GetComponent<Tilemap>();
            tilemap.CompressBounds();

            var cells = new List<Vector3Int>();
            foreach (var cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) is DoorTile)
                    cells.Add(cell);
            }
            if (cells.Count == 0) return;

            var byColumn = new Dictionary<int, List<Vector3Int>>();
            for (int i = 0; i < cells.Count; i++)
            {
                int x = cells[i].x;
                if (!byColumn.TryGetValue(x, out var column))
                {
                    column = new List<Vector3Int>();
                    byColumn[x] = column;
                }
                column.Add(cells[i]);
            }

            var xs = new List<int>(byColumn.Keys);
            xs.Sort();

            var gates = new List<DoorGate>();
            for (int i = 0; i < xs.Count; i++)
            {
                var column = byColumn[xs[i]];
                int top = int.MinValue;
                for (int c = 0; c < column.Count; c++)
                    if (column[c].y > top) top = column[c].y;

                var gate = new DoorGate();
                for (int c = 0; c < column.Count; c++)
                    gate.Blocks.Add(DoorBlock.Create(tilemap, column[c], column[c].y == top, transform));

                Vector3 corner = tilemap.CellToWorld(new Vector3Int(xs[i], 0, 0));
                gate.MinX = corner.x;
                gate.MaxX = corner.x + tilemap.cellSize.x;
                gates.Add(gate);
            }

            if (gates.Count == 1)
            {
                rooms.Add(new ArenaRoom(gates[0], null, gates[0].MaxX, float.PositiveInfinity));
            }
            else
            {
                for (int i = 0; i < gates.Count - 1; i++)
                    rooms.Add(new ArenaRoom(gates[i], gates[i + 1], gates[i].MaxX, gates[i + 1].MinX));
            }

            for (int i = 0; i < cells.Count; i++)
                tilemap.SetTile(cells[i], null);
        }

        private void Update()
        {
            if (rooms.Count == 0) return;
            if (playerBody == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;
                playerBody = player.GetComponent<Collider2D>();
                if (playerBody == null) return;
            }

            for (int i = 0; i < rooms.Count; i++)
                rooms[i].Tick(playerBody.bounds);
        }
    }

    internal class DoorGate
    {
        public readonly List<DoorBlock> Blocks = new List<DoorBlock>();
        public float MinX;
        public float MaxX;
        private int lockCount;

        public void Lock()
        {
            lockCount++;
            if (lockCount == 1)
            {
                for (int i = 0; i < Blocks.Count; i++) Blocks[i].Close();
                Blocks[0].PlaySlam();
            }
        }

        public void Unlock()
        {
            if (lockCount == 0) return;
            lockCount--;
            if (lockCount == 0)
            {
                for (int i = 0; i < Blocks.Count; i++) Blocks[i].Open();
            }
        }
    }

    internal class ArenaRoom
    {
        private readonly DoorGate left;
        private readonly DoorGate right;
        private readonly float innerMin;
        private readonly float innerMax;
        private readonly List<EnemyActor> occupants = new List<EnemyActor>();
        private bool locked;
        private bool cleared;

        public ArenaRoom(DoorGate left, DoorGate right, float innerMin, float innerMax)
        {
            this.left = left;
            this.right = right;
            this.innerMin = innerMin;
            this.innerMax = innerMax;
        }

        public void Tick(Bounds player)
        {
            if (cleared) return;

            bool inside = player.min.x > innerMin && player.max.x < innerMax;
            if (!locked && inside)
                TryLock();

            if (locked && AllDead())
            {
                cleared = true;
                left.Unlock();
                right?.Unlock();
            }
        }

        private void TryLock()
        {
            occupants.Clear();
            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead) continue;
                float x = enemy.transform.position.x;
                if (x > innerMin && x < innerMax) occupants.Add(enemy);
            }

            if (occupants.Count == 0)
            {
                cleared = true;
                return;
            }

            locked = true;
            left.Lock();
            right?.Lock();
        }

        private bool AllDead()
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                var enemy = occupants[i];
                if (enemy != null && !enemy.IsDead) return false;
            }
            return true;
        }
    }

    internal class DoorBlock : MonoBehaviour
    {
        private static Sprite frameSprite;
        private static Sprite shutterSprite;
        private static Sprite lampSprite;
        private static AudioClip slamClip;

        private Transform shutter;
        private BoxCollider2D body;
        private SpriteRenderer lamp;
        private float openY;
        private float targetY;
        private float speed;

        public static DoorBlock Create(Tilemap tilemap, Vector3Int cell, bool top, Transform parent)
        {
            EnsureSprites();
            Vector3 size = tilemap.cellSize;
            var go = new GameObject("DoorBlock");
            go.transform.SetParent(parent, false);
            go.transform.position = tilemap.CellToWorld(cell);

            int ground = LayerMask.NameToLayer("Ground");
            if (ground < 0) ground = 0;
            go.layer = ground;

            var block = go.AddComponent<DoorBlock>();
            block.openY = size.y;
            block.targetY = size.y;
            block.speed = size.y / 0.12f;

            var frame = NewSprite("Frame", go.transform, frameSprite, 6);
            frame.transform.localScale = new Vector3(size.x, size.y, 1f);

            var shutterGo = NewSprite("Shutter", go.transform, shutterSprite, 7);
            shutterGo.transform.localScale = frame.transform.localScale;
            shutterGo.transform.localPosition = new Vector3(0f, size.y, 0f);
            shutterGo.GetComponent<SpriteRenderer>().enabled = false;
            block.shutter = shutterGo.transform;

            if (top)
            {
                var lampGo = NewSprite("Lamp", go.transform, lampSprite, 8);
                lampGo.transform.localPosition = new Vector3(size.x * 0.5f, size.y - 0.2f, 0f);
                block.lamp = lampGo.GetComponent<SpriteRenderer>();
                block.lamp.color = new Color(0.35f, 0.95f, 0.45f);
            }

            block.body = go.AddComponent<BoxCollider2D>();
            block.body.size = new Vector2(size.x, size.y);
            block.body.offset = new Vector2(size.x * 0.5f, size.y * 0.5f);
            block.body.enabled = false;
            return block;
        }

        public void Close()
        {
            targetY = 0f;
            speed = openY / 0.1f;
            body.enabled = true;
            if (lamp != null) lamp.color = new Color(1f, 0.28f, 0.22f);
        }

        public void Open()
        {
            targetY = openY;
            speed = openY / 0.18f;
            body.enabled = false;
            if (lamp != null) lamp.color = new Color(0.35f, 0.95f, 0.45f);
        }

        public void PlaySlam()
        {
            if (slamClip == null) slamClip = MakeSlam();
            var source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 0f;
            source.playOnAwake = false;
            source.PlayOneShot(slamClip, 0.85f);
        }

        private void Update()
        {
            if (shutter == null) return;
            var pos = shutter.localPosition;
            pos.y = Mathf.MoveTowards(pos.y, targetY, speed * Time.deltaTime);
            shutter.localPosition = pos;
            var renderer = shutter.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = pos.y < openY - 0.02f;
        }

        private static GameObject NewSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return go;
        }

        private static void EnsureSprites()
        {
            if (frameSprite == null) frameSprite = MakeFrame();
            if (shutterSprite == null) shutterSprite = MakeShutter();
            if (lampSprite == null) lampSprite = MakeLamp();
        }

        private static Sprite MakeFrame()
        {
            var px = new Color32[16 * 16];
            var edge = new Color32(18, 28, 58, 255);
            var metal = new Color32(46, 78, 138, 255);
            var trim = new Color32(168, 196, 230, 255);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if (x > 1 && x < 14) continue;
                    bool bolt = y % 5 == 2 && x != 0 && x != 15;
                    px[y * 16 + x] = bolt ? trim : x == 0 || x == 15 ? edge : metal;
                }
            }
            return SpriteFrom(px, new Vector2(0f, 0f), "DoorFrame");
        }

        private static Sprite MakeShutter()
        {
            var px = new Color32[16 * 16];
            var dark = new Color32(24, 36, 72, 255);
            var plate = new Color32(62, 104, 176, 255);
            var shine = new Color32(186, 214, 242, 255);
            var warn = new Color32(232, 186, 48, 255);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 2; x < 14; x++)
                {
                    bool stripe = y >= 6 && y <= 8;
                    px[y * 16 + x] = stripe ? warn : y % 4 == 0 ? dark : x == 3 ? shine : plate;
                }
            }
            return SpriteFrom(px, new Vector2(0f, 0f), "DoorShutter");
        }

        private static Sprite MakeLamp()
        {
            var px = new Color32[4 * 4];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            var tex = NewTexture(4, 4, px, "DoorLamp");
            var sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 16f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Sprite SpriteFrom(Color32[] pixels, Vector2 pivot, string spriteName)
        {
            var tex = NewTexture(16, 16, pixels, spriteName);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), pivot, 16f, 0, SpriteMeshType.FullRect);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Texture2D NewTexture(int width, int height, Color32[] pixels, string textureName)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        private static AudioClip MakeSlam()
        {
            const int rate = 22050;
            int n = rate / 10;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)n;
                float body = Mathf.Sin(u * 48f * Mathf.PI) * Mathf.Exp(-u * 10f);
                float knock = (Mathf.PerlinNoise(u * 18f, 0.2f) * 2f - 1f) * Mathf.Exp(-u * 16f);
                data[i] = (body * 0.7f + knock * 0.3f) * 0.85f;
            }
            var clip = AudioClip.Create("ArenaDoorSlam", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
