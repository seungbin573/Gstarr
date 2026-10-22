using System.Collections.Generic;
using Gstarr.Combat;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.World
{
    /// <summary>
    /// Doors 타일맵의 칸을 문으로 만든다.
    /// 바닥 칸 하나가 문이고, 그림은 위로 3칸이다.
    /// 문 그림은 Assets/_Project/Tilemaps/DoorTiles/Door.png 를 바꾸면 바뀐다.
    /// 같은 x의 칸이 문 하나고, 이웃한 두 문 사이가 방이다.
    /// 방에 들어가면 양쪽 문이 잠기고, 그 안의 적을 모두 처치하면 열린다.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    [DefaultExecutionOrder(100)]
    public class ArenaDoorSpawner : MonoBehaviour
    {
        public const int DefaultHeightTiles = 3;

        [SerializeField] Sprite doorSprite;
        [SerializeField] Sprite frameSprite;
        [SerializeField] Sprite lampSprite;
        [SerializeField] int heightInTiles = DefaultHeightTiles;

        private readonly List<ArenaRoom> rooms = new List<ArenaRoom>();
        private readonly HashSet<BossActor> configuredBosses = new HashSet<BossActor>();
        private Collider2D playerBody;

        public void AssignArt(Sprite door, Sprite frame, Sprite lamp)
        {
            doorSprite = door;
            frameSprite = frame;
            lampSprite = lamp;
        }

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
            if (cells.Count == 0)
            {
                ActivateUnboundedBosses();
                return;
            }

            if (doorSprite == null)
            {
                Debug.LogWarning("문 이미지가 없습니다. Assets/_Project/Tilemaps/DoorTiles/Door.png 를 연결하세요.", this);
                ActivateUnboundedBosses();
                return;
            }

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
            int tilesTall = Mathf.Max(1, heightInTiles);

            var gates = new List<DoorGate>();
            for (int i = 0; i < xs.Count; i++)
            {
                var column = byColumn[xs[i]];
                int bottom = int.MaxValue;
                for (int c = 0; c < column.Count; c++)
                    if (column[c].y < bottom) bottom = column[c].y;

                var gate = new DoorGate();
                gate.Blocks.Add(DoorBlock.Create(
                    tilemap, new Vector3Int(xs[i], bottom, 0), tilesTall,
                    doorSprite, frameSprite, lampSprite, transform));

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

            ConfigureBosses();

            for (int i = 0; i < cells.Count; i++)
                tilemap.SetTile(cells[i], null);
        }

        private void ConfigureBosses()
        {
            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] == null || enemies[i].IsDead) continue;
                var boss = enemies[i].GetComponent<BossActor>();
                if (boss == null || configuredBosses.Contains(boss)) continue;

                float x = enemies[i].transform.position.x;
                bool registered = false;
                for (int r = 0; r < rooms.Count; r++)
                {
                    if (!rooms[r].Contains(x)) continue;
                    rooms[r].RegisterBoss(boss);
                    registered = true;
                    break;
                }
                if (!registered) boss.SetEncounterActive(true);
                configuredBosses.Add(boss);
            }
        }

        private static void ActivateUnboundedBosses()
        {
            var bosses = Object.FindObjectsByType<BossActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < bosses.Length; i++)
                if (bosses[i] != null) bosses[i].SetEncounterActive(true);
        }

        private void Update()
        {
            if (rooms.Count == 0)
            {
                ActivateUnboundedBosses();
                return;
            }

            ConfigureBosses();
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

        public bool Contains(float x) => x >= innerMin && x <= innerMax;

        public void RegisterBoss(BossActor boss)
        {
            if (cleared)
            {
                cleared = false;
                locked = false;
                occupants.Clear();
            }
            var actor = boss.GetComponent<EnemyActor>();
            if (actor != null && !occupants.Contains(actor)) occupants.Add(actor);
            boss.SetArenaBounds(innerMin, innerMax);
            boss.SetEncounterActive(locked);
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
            for (int i = 0; i < occupants.Count; i++)
            {
                var boss = occupants[i] != null ? occupants[i].GetComponent<BossActor>() : null;
                if (boss != null) boss.SetEncounterActive(true);
            }
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
        private static AudioClip slamClip;

        private Transform shutter;
        private BoxCollider2D body;
        private SpriteRenderer lamp;
        private float openY;
        private float targetY;
        private float speed;

        public static DoorBlock Create(
            Tilemap tilemap, Vector3Int bottomCell, int heightTiles,
            Sprite doorSprite, Sprite frameSprite, Sprite lampSprite, Transform parent)
        {
            float cellW = tilemap.cellSize.x;
            float cellH = tilemap.cellSize.y;
            float targetW = cellW;
            float targetH = cellH * heightTiles;

            Vector3 corner = tilemap.CellToWorld(bottomCell);
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.position = corner + new Vector3(targetW * 0.5f, 0f, 0f);

            int ground = LayerMask.NameToLayer("Ground");
            if (ground < 0) ground = 0;
            go.layer = ground;

            var block = go.AddComponent<DoorBlock>();
            block.openY = targetH;
            block.targetY = targetH;
            block.speed = targetH / 0.12f;

            if (frameSprite != null)
            {
                var frame = NewSprite("Frame", go.transform, frameSprite, 6);
                frame.transform.localScale = Fit(frameSprite, targetW, targetH);
            }

            var shutterGo = NewSprite("Shutter", go.transform, doorSprite, 7);
            shutterGo.transform.localScale = Fit(doorSprite, targetW, targetH);
            shutterGo.transform.localPosition = new Vector3(0f, targetH, 0f);
            shutterGo.GetComponent<SpriteRenderer>().enabled = false;
            block.shutter = shutterGo.transform;

            if (lampSprite != null)
            {
                const float lampSize = 0.28f;
                var lampGo = NewSprite("Lamp", go.transform, lampSprite, 8);
                lampGo.transform.localScale = Fit(lampSprite, lampSize, lampSize);
                lampGo.transform.localPosition = new Vector3(0f, targetH - lampSize * 0.35f, 0f);
                block.lamp = lampGo.GetComponent<SpriteRenderer>();
                block.lamp.color = new Color(0.35f, 0.95f, 0.45f);
            }

            block.body = go.AddComponent<BoxCollider2D>();
            block.body.size = new Vector2(targetW, targetH);
            block.body.offset = new Vector2(0f, targetH * 0.5f);
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

        private static Vector3 Fit(Sprite sprite, float width, float height)
        {
            Vector2 size = sprite.bounds.size;
            float sx = size.x > 0.0001f ? width / size.x : 1f;
            float sy = size.y > 0.0001f ? height / size.y : 1f;
            return new Vector3(sx, sy, 1f);
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
