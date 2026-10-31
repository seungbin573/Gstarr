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
        private static readonly List<Vector2> doorBarriers = new List<Vector2>();
        private static ArenaDoorSpawner active;

        public static bool HasUnopenedRooms
        {
            get
            {
                if (active == null) return false;
                for (int i = 0; i < active.rooms.Count; i++)
                    if (!active.rooms[i].IsOpened) return true;
                return false;
            }
        }

        /// <summary>fromX에서 toX로 갈 때 문 칸을 넘지 못하게 막는다. x는 문 바깥쪽 가장자리다.</summary>
        public static float LimitCrossing(float fromX, float toX, float padding)
        {
            if (doorBarriers.Count == 0 || Mathf.Abs(toX - fromX) < 0.0001f) return toX;
            float stop = toX;
            if (toX > fromX)
            {
                for (int i = 0; i < doorBarriers.Count; i++)
                {
                    float edge = doorBarriers[i].x - padding;
                    if (fromX < edge && stop > edge) stop = edge;
                }
            }
            else
            {
                for (int i = 0; i < doorBarriers.Count; i++)
                {
                    float edge = doorBarriers[i].y + padding;
                    if (fromX > edge && stop < edge) stop = edge;
                }
            }
            return stop;
        }

        /// <summary>두 위치 사이에 문 칸이 있으면 원거리 적이 서로를 보면 안 된다.</summary>
        public static bool SeparatedByDoor(float a, float b)
        {
            float left = Mathf.Min(a, b);
            float right = Mathf.Max(a, b);
            for (int i = 0; i < doorBarriers.Count; i++)
            {
                float doorL = doorBarriers[i].x;
                float doorR = doorBarriers[i].y;
                if (doorR > left + 0.08f && doorL < right - 0.08f) return true;
            }
            return false;
        }

        /// <summary>발사 방향에서 다음 문까지의 거리. 탄이 문을 넘어가지 않게 한다.</summary>
        public static float RangeUntilDoor(float originX, float directionX, float maxRange)
        {
            if (Mathf.Abs(directionX) < 0.02f) return maxRange;
            float range = maxRange;
            for (int i = 0; i < doorBarriers.Count; i++)
            {
                if (directionX > 0f)
                {
                    float doorL = doorBarriers[i].x;
                    if (doorL > originX + 0.05f) range = Mathf.Min(range, doorL - originX);
                }
                else
                {
                    float doorR = doorBarriers[i].y;
                    if (doorR < originX - 0.05f) range = Mathf.Min(range, originX - doorR);
                }
            }
            return range;
        }

        private Collider2D playerBody;
        private CameraSystem.CameraFollow2D cameraFollow;

        private void Awake()
        {
            active = this;
            var renderer = GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (active == this) active = null;
        }

        public void AssignArt(Sprite door, Sprite frame, Sprite lamp)
        {
            doorSprite = door;
            frameSprite = frame;
            lampSprite = lamp;
        }

        private void Start()
        {
            doorBarriers.Clear();
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
                doorBarriers.Add(new Vector2(gate.MinX, gate.MaxX));
            }

            if (gates.Count == 1)
            {
                AddRoom(new ArenaRoom(gates[0], null, gates[0].MaxX, gates[0].MaxX + 24f, gates[0].FloorY));
            }
            else
            {
                for (int i = 0; i < gates.Count - 1; i++)
                {
                    float floor = Mathf.Min(gates[i].FloorY, gates[i + 1].FloorY);
                    AddRoom(new ArenaRoom(gates[i], gates[i + 1], gates[i].MaxX, gates[i + 1].MinX, floor));
                }
            }

            if (RunCheckpoint.TryConsume(gameObject.scene.name, out Vector3 stand, out float floorY, out int clearedThrough))
            {
                for (int i = 0; i <= clearedThrough && i < rooms.Count; i++)
                    rooms[i].MarkCleared();
                int next = clearedThrough + 1;
                if (next < rooms.Count) OpenRoom(next);
                ResumePlayer(stand);
                CreateBeacon(stand.x, floorY);
            }
            else if (rooms.Count > 0)
            {
                OpenRoom(0);
            }

            ConfigureBosses();

            for (int i = 0; i < cells.Count; i++)
            {
                tilemap.SetTileFlags(cells[i], TileFlags.None);
                tilemap.SetTile(cells[i], null);
            }
            tilemap.RefreshAllTiles();
        }

        private void AddRoom(ArenaRoom room)
        {
            room.Assign(rooms.Count, OnRoomCleared);
            rooms.Add(room);
        }

        private void OnRoomCleared(ArenaRoom room)
        {
            if (room.HostsBoss)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                float standY = player != null ? player.transform.position.y : room.FrameY + 2.2f;
                var stand = new Vector3(room.BossPoint.x, standY, 0f);
                RunCheckpoint.Save(gameObject.scene.name, stand, room.FrameY, room.Index);
                CreateBeacon(room.BossPoint.x, room.FrameY);
                StageHUD.ShowHint("보스를 쓰러뜨렸습니다. 이곳에 세이브 지점을 남깁니다. 이후 죽으면 여기서 이어 할 수 있고, 점수는 0이 됩니다.", 5.5f);
            }

            int next = room.Index + 1;
            bool more = next < rooms.Count;
            if (more) OpenRoom(next);
            StageHUD.AnnounceRoomClear(more);
        }

        private void OpenRoom(int index)
        {
            if (index < 0 || index >= rooms.Count) return;
            var room = rooms[index];
            if (room.IsOpened) return;
            room.MarkOpened();

            float span = room.FrameMax - room.FrameMin;
            bool wide = span >= 19f;
            bool bossRoom = index == 4 && rooms.Count >= 5;
            if (bossRoom)
            {
                SpawnInto(room, EnemyKind.Boss, 0.55f, 0f);
                return;
            }

            if (!wide)
            {
                SpawnInto(room, NarrowKind(index), 0.55f, NarrowKind(index) == EnemyKind.Flyer ? 4f : 0f);
                return;
            }

            var pair = WidePair(index);
            SpawnInto(room, pair.a, 0.14f, pair.a == EnemyKind.Flyer ? 4f : 0f);
            SpawnInto(room, pair.b, 0.9f, pair.b == EnemyKind.Flyer ? 4f : 0f);
        }

        private static EnemyKind NarrowKind(int index)
        {
            EnemyKind[] kinds =
            {
                EnemyKind.BeatChaser,
                EnemyKind.Mole,
                EnemyKind.Flyer,
                EnemyKind.Hopper,
                EnemyKind.RangedHalfBeat
            };
            return kinds[Mathf.Abs(index) % kinds.Length];
        }

        private static (EnemyKind a, EnemyKind b) WidePair(int index)
        {
            (EnemyKind a, EnemyKind b)[] pairs =
            {
                (EnemyKind.RangedBeat, EnemyKind.Mole),
                (EnemyKind.Flyer, EnemyKind.Hopper),
                (EnemyKind.BeatChaser, EnemyKind.RangedHalfBeat),
                (EnemyKind.Hopper, EnemyKind.RangedBeat),
                (EnemyKind.RangedHalfBeat, EnemyKind.Mole),
                (EnemyKind.Flyer, EnemyKind.BeatChaser)
            };
            return pairs[Mathf.Abs(index) % pairs.Length];
        }

        private static void SpawnInto(ArenaRoom room, EnemyKind kind, float along, float lift)
        {
            float x = Mathf.Lerp(room.FrameMin + 1.2f, room.FrameMax - 1.2f, along);
            var position = new Vector3(x, room.FrameY + lift, 0f);
            EnemyActor actor = kind == EnemyKind.Boss
                ? EnemyActor.SpawnBoss(position)
                : EnemyActor.Spawn(null, position, kind);
            if (kind == EnemyKind.Boss) room.NoteBoss(position);
            actor.WaitForPlayerMove();
        }

        private static void ResumePlayer(Vector3 stand)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            player.transform.position = stand;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = stand;
                body.linearVelocity = Vector2.zero;
            }

            var camera = Camera.main;
            if (camera != null)
            {
                var follow = camera.GetComponent<CameraSystem.CameraFollow2D>();
                if (follow != null) follow.SnapTo(new Vector3(stand.x, stand.y + 1.5f, camera.transform.position.z));
                else
                {
                    var camPos = camera.transform.position;
                    camera.transform.position = new Vector3(stand.x, stand.y + 1.5f, camPos.z);
                }
            }
        }

        private static Sprite beaconSprite;

        private static void CreateBeacon(float x, float floorY)
        {
            var go = new GameObject("SaveBeacon");
            go.transform.position = new Vector3(x, floorY, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = BeaconSprite();
            renderer.color = new Color(1f, 0.82f, 0.28f);
            renderer.sortingOrder = 8;
            go.AddComponent<SaveBeacon>();
        }

        private static Sprite BeaconSprite()
        {
            if (beaconSprite != null) return beaconSprite;
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int px = 0; px < size; px++)
                {
                    int dx = Mathf.Abs(px - 7);
                    int dy = Mathf.Abs(y - 6);
                    bool diamond = dx + dy <= 6 && y > 2;
                    bool pole = px >= 7 && px <= 8 && y <= 6;
                    tex.SetPixel(px, y, diamond || pole ? Color.white : clear);
                }
            }
            tex.Apply();
            beaconSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0f), 16f);
            return beaconSprite;
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
            if (RunCheckpoint.SuppressBossTheme)
            {
                if (!inBossRoom) RunCheckpoint.ReleaseBossTheme();
                else
                {
                    conductor.SetBossTheme(stage.bossMusic, false);
                    return;
                }
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
        private System.Action<ArenaRoom> onCleared;
        private bool locked;
        private bool cleared;
        private bool opened;

        public ArenaRoom(DoorGate left, DoorGate right, float innerMin, float innerMax, float floorY)
        {
            this.left = left;
            this.right = right;
            this.innerMin = innerMin;
            this.innerMax = innerMax;
            this.floorY = floorY;
        }

        public int Index { get; private set; }
        public bool IsOpened => opened;
        public bool HostsBoss { get; private set; }
        public Vector3 BossPoint { get; private set; }

        public void Assign(int index, System.Action<ArenaRoom> cleared)
        {
            Index = index;
            onCleared = cleared;
        }

        public void MarkOpened() => opened = true;

        public void MarkCleared()
        {
            cleared = true;
            opened = true;
            locked = false;
        }

        public void NoteBoss(Vector3 point)
        {
            HostsBoss = true;
            BossPoint = point;
        }

        public bool Contains(float x) => x >= innerMin && x <= innerMax;
        public bool HasBoss => anchored.Count > 0 || HostsBoss;
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
                onCleared?.Invoke(this);
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

    /// <summary>보스 자리에 남는 세이브 표식. 위아래로 살짝 뜬다.</summary>
    internal class SaveBeacon : MonoBehaviour
    {
        private Vector3 origin;

        private void Awake() => origin = transform.position;

        private void Update()
        {
            float bob = Mathf.Sin(Time.time * 2.4f) * 0.12f;
            transform.position = origin + Vector3.up * (0.15f + bob);
        }
    }
}
