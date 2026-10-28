using System.Collections.Generic;
using Gstarr.Combat;
using Gstarr.Game;
using Gstarr.Rhythm;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Gstarr.World
{
    /// <summary>
    /// Doors 타일맵의 칸을 문으로 만든다.
    /// 바닥 칸 하나가 문이고, 그림은 위로 3칸이다.
    /// 문 그림은 Assets/_Project/Tilemaps/DoorTiles/Door.png 를 바꾸면 바뀐다.
    /// 같은 x의 칸이 문 하나고, 이웃한 두 문 사이가 방이다.
    /// 그 문 칸을 밟으면 잠기고, 그 높이의 적을 모두 처치하면 열린다.
    /// 문 아래층을 지나가는 것만으로는 시작하지 않는다.
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
        private readonly List<DoorGate> gates = new List<DoorGate>();
        private Collider2D playerBody;
        private CameraSystem.CameraFollow2D cameraFollow;

        private void Awake()
        {
            var renderer = GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

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

            gates.Clear();
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

                Vector3 corner = tilemap.CellToWorld(new Vector3Int(xs[i], bottom, 0));
                gate.MinX = corner.x;
                gate.MaxX = corner.x + tilemap.cellSize.x;
                gate.FloorY = corner.y;
                gate.OpeningHeight = tilemap.cellSize.y * tilesTall;
                gates.Add(gate);
            }

            if (gates.Count == 1)
            {
                rooms.Add(new ArenaRoom(gates[0], null, gates[0].MaxX, float.PositiveInfinity, gates[0].FloorY));
            }
            else
            {
                for (int i = 0; i < gates.Count - 1; i++)
                {
                    float floor = Mathf.Min(gates[i].FloorY, gates[i + 1].FloorY);
                    rooms.Add(new ArenaRoom(gates[i], gates[i + 1], gates[i].MaxX, gates[i + 1].MinX, floor));
                }
            }

            ConfigureBosses();

            for (int i = 0; i < cells.Count; i++)
            {
                tilemap.SetTileFlags(cells[i], TileFlags.None);
                tilemap.SetTile(cells[i], null);
            }
            tilemap.RefreshAllTiles();
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
                ArenaRoom best = null;
                float bestDist = float.PositiveInfinity;
                for (int r = 0; r < rooms.Count; r++)
                {
                    float dist = rooms[r].DistanceTo(x);
                    if (dist >= bestDist) continue;
                    bestDist = dist;
                    best = rooms[r];
                }
                if (best != null) best.RegisterBoss(boss);
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
                rooms[i].Tick(playerBody);
            AssignLeashes();
            UpdateCameraFrame(playerBody.bounds);
        }

        private void AssignLeashes()
        {
            if (gates.Count == 0) return;
            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.HasLeash || enemy.IsBoss) continue;
                float x = enemy.transform.position.x;
                ArenaRoom room = null;
                for (int r = 0; r < rooms.Count; r++)
                {
                    if (!rooms[r].Contains(x)) continue;
                    room = rooms[r];
                    break;
                }

                if (room != null) enemy.SetLeash(room.FrameMin, room.FrameMax);
                else if (x < gates[0].MinX) enemy.SetLeash(gates[0].MinX - 30f, gates[0].MinX);
                else enemy.SetLeash(gates[gates.Count - 1].MaxX, gates[gates.Count - 1].MaxX + 8f);
            }
        }

        private void UpdateCameraFrame(Bounds player)
        {
            if (gates.Count == 0 || rooms.Count == 0) return;
            if (cameraFollow == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                cameraFollow = cam.GetComponent<CameraSystem.CameraFollow2D>();
                if (cameraFollow == null) return;
            }

            float x = player.center.x;
            int index = -1;
            for (int i = 0; i < rooms.Count; i++)
                if (x >= rooms[i].FrameMin) index = i;

            if (index < 0)
            {
                // 첫 문 앞에서 카메라를 플레이어 쪽에 두면 왼쪽 절반이 빈다. 스테이지가 보이도록 오른쪽으로 늘린다.
                cameraFollow.SetFrame(gates[0].MinX - 12f, gates[0].MinX + 14f, gates[0].FloorY + 2.6f);
                UpdateBossMusic(x);
                return;
            }

            cameraFollow.SetFrame(rooms[index].FrameMin, rooms[index].FrameMax, rooms[index].FrameY + 2.6f);
            UpdateBossMusic(x);
        }

        private void UpdateBossMusic(float x)
        {
            var stage = StageSession.Current;
            var conductor = RhythmConductor.Instance;
            if (stage == null || conductor == null) return;
            bool inBossRoom = false;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (!rooms[i].HasBoss) continue;
                if (x >= rooms[i].FrameMin && x <= rooms[i].FrameMax) inBossRoom = true;
            }
            conductor.SetBossTheme(stage.bossMusic, inBossRoom);
        }
    }

    internal class DoorGate
    {
        public readonly List<DoorBlock> Blocks = new List<DoorBlock>();
        public float MinX;
        public float MaxX;
        public float FloorY;
        public float OpeningHeight;
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
        private readonly float floorY;
        private readonly List<EnemyActor> occupants = new List<EnemyActor>();
        private readonly List<EnemyActor> anchored = new List<EnemyActor>();
        private bool locked;
        private bool cleared;

        public ArenaRoom(DoorGate left, DoorGate right, float innerMin, float innerMax, float floorY)
        {
            this.left = left;
            this.right = right;
            this.innerMin = innerMin;
            this.innerMax = innerMax;
            this.floorY = floorY;
        }

        public bool Contains(float x) => x >= innerMin && x <= innerMax;
        public bool HasBoss => anchored.Count > 0;
        public float FrameMin => innerMin;
        public float FrameMax => innerMax;
        public float FrameY => floorY;

        public float DistanceTo(float x)
        {
            if (x < innerMin) return innerMin - x;
            if (x > innerMax) return x - innerMax;
            return 0f;
        }

        public void RegisterBoss(BossActor boss)
        {
            if (cleared)
            {
                cleared = false;
                locked = false;
                occupants.Clear();
            }
            var actor = boss.GetComponent<EnemyActor>();
            if (actor != null && !anchored.Contains(actor)) anchored.Add(actor);
            boss.SetArenaBounds(innerMin, innerMax, floorY);
            boss.SetEncounterActive(locked);
        }

        public void Tick(Collider2D player)
        {
            if (player == null || cleared) return;

            var bounds = player.bounds;
            if (!locked && HasEntered(bounds))
                TryLock(player);
            if (locked)
                KeepInside(player, force: false);

            if (locked && AllDead())
            {
                cleared = true;
                left.Unlock();
                right?.Unlock();
                StageHUD.AnnounceRoomClear();
            }
        }

        /// <summary>문 칸을 스치거나 문 위에 선 것은 입장이 아니다. 문 안쪽 바닥에 들어온 것만 잠근다.</summary>
        private bool HasEntered(Bounds player)
        {
            float x = player.center.x;
            if (x <= innerMin + 0.12f) return false;
            if (right != null && x >= innerMax - 0.12f) return false;

            float feet = player.min.y;
            return feet > floorY - 0.2f && feet < floorY + left.OpeningHeight - 0.4f;
        }

        private void TryLock(Collider2D player)
        {
            occupants.Clear();
            for (int i = 0; i < anchored.Count; i++)
            {
                var boss = anchored[i];
                if (boss != null && !boss.IsDead) occupants.Add(boss);
            }

            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsDead || occupants.Contains(enemy)) continue;
                float x = enemy.transform.position.x;
                if (x <= innerMin || x >= innerMax) continue;
                if (enemy.transform.position.y < floorY - 0.75f) continue;
                occupants.Add(enemy);
            }

            if (occupants.Count == 0)
            {
                cleared = true;
                return;
            }

            locked = true;
            KeepInside(player, force: true);
            Physics2D.SyncTransforms();
            left.Lock();
            right?.Lock();
            StageHUD.HideAdvance();
            for (int i = 0; i < occupants.Count; i++)
            {
                var boss = occupants[i] != null ? occupants[i].GetComponent<BossActor>() : null;
                if (boss != null) boss.SetEncounterActive(true);
            }
        }

        private void KeepInside(Collider2D player, bool force)
        {
            float half = player.bounds.extents.x;
            float min = innerMin + half + 0.08f;
            float max = right != null ? innerMax - half - 0.08f : float.PositiveInfinity;
            if (min > max) min = max = (innerMin + innerMax) * 0.5f;

            var body = player.attachedRigidbody;
            float x = body != null ? body.position.x : player.transform.position.x;
            // 문에 닿아 있는 것은 그대로 둔다. 문을 통과해 밖으로 나갔을 때만 끌어온다.
            if (!force && x >= min - 0.2f && x <= max + 0.2f) return;

            float clamped = Mathf.Clamp(x, min, max);
            if (Mathf.Abs(clamped - x) < 0.001f) return;

            if (body != null)
            {
                body.position = new Vector2(clamped, body.position.y);
                var velocity = body.linearVelocity;
                if (clamped > x && velocity.x < 0f) velocity.x = 0f;
                if (clamped < x && velocity.x > 0f) velocity.x = 0f;
                body.linearVelocity = velocity;
            }

            var position = player.transform.position;
            position.x = clamped;
            player.transform.position = position;
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

        private Transform shutter;
        private Sprite shutterSprite;
        private Collider2D body;
        private SpriteRenderer lamp;
        private float openY;
        private float targetY;
        private float shutterBottom;
        private float shutterWidth;
        private float shutterHeight;
        private float speed;
        private bool sealedDoor;

        public static DoorBlock Create(
            Tilemap tilemap, Vector3Int bottomCell, int heightTiles,
            Sprite doorSprite, Sprite frameSprite, Sprite lampSprite, Transform parent)
        {
            float cellW = tilemap.cellSize.x;
            float cellH = tilemap.cellSize.y;
            float targetW = cellW;
            int frameTiles = Mathf.Max(1, heightTiles);
            float frameH = cellH * frameTiles;

            Vector3 corner = tilemap.CellToWorld(bottomCell);
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.position = corner + new Vector3(targetW * 0.5f, 0f, 0f);

            // 바닥 레이어에 두면 부딪힌 순간 땅으로 인식되어 벽에 붙는다.
            go.layer = 0;

            var block = go.AddComponent<DoorBlock>();
            block.openY = frameH;
            block.targetY = frameH;
            block.shutterBottom = frameH;
            block.shutterWidth = targetW;
            block.shutterHeight = frameH;
            block.shutterSprite = doorSprite;
            block.speed = frameH / 0.12f;

            var shutterGo = NewSprite("Shutter", go.transform, doorSprite, 7);
            block.shutter = shutterGo.transform;
            block.SetShutterBottom(frameH);
            shutterGo.GetComponent<SpriteRenderer>().enabled = false;

            // 바닥 이음새와 윗면에 걸리지 않는 세로 벽. 점프로는 위에 올라설 수 없다.
            var box = go.AddComponent<BoxCollider2D>();
            float wallHeight = frameH + 1.1f;
            box.size = new Vector2(Mathf.Max(0.45f, targetW * 0.72f), wallHeight);
            box.offset = new Vector2(0f, 0.18f + wallHeight * 0.5f);
            box.sharedMaterial = new PhysicsMaterial2D("DoorSlip") { friction = 0f, bounciness = 0f };
            box.enabled = false;
            block.body = box;
            return block;
        }

        public void Close()
        {
            targetY = 0f;
            speed = openY / 0.16f;
            sealedDoor = true;
            if (body != null) body.enabled = true;
            if (lamp != null) lamp.color = new Color(1f, 0.28f, 0.22f);
        }

        public void Open()
        {
            targetY = openY;
            speed = openY / 0.2f;
            sealedDoor = false;
            if (body != null) body.enabled = false;
            if (lamp != null) lamp.color = new Color(0.35f, 0.95f, 0.45f);
        }

        public void PlaySlam()
        {
            CombatFeel.Shake(0.08f, 0.08f);
        }

        private void Update()
        {
            if (shutter == null) return;
            shutterBottom = Mathf.MoveTowards(shutterBottom, targetY, speed * Time.deltaTime);
            SetShutterBottom(shutterBottom);
            if (body != null) body.enabled = sealedDoor;
            var renderer = shutter.GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private void SetShutterBottom(float bottom)
        {
            if (shutter == null || shutterSprite == null) return;
            ApplyBottomCenter(shutter, shutterSprite, shutterWidth, shutterHeight, bottom);
        }

        private static void ApplyBottomCenter(Transform target, Sprite sprite, float width, float height, float bottom)
        {
            target.localScale = Fit(sprite, width, height);
            var bounds = sprite.bounds;
            target.localPosition = new Vector3(
                -bounds.center.x * target.localScale.x,
                bottom - bounds.min.y * target.localScale.y,
                0f);
        }

        private static void ApplyCenter(Transform target, Sprite sprite, float width, float height, Vector2 center)
        {
            target.localScale = Fit(sprite, width, height);
            var bounds = sprite.bounds;
            target.localPosition = new Vector3(
                center.x - bounds.center.x * target.localScale.x,
                center.y - bounds.center.y * target.localScale.y,
                0f);
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

    }
}
