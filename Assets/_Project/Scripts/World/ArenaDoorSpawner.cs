using System.Collections.Generic;
using Gstarr.Combat;
using Gstarr.Game;
using Gstarr.Player;
using Gstarr.Rhythm;
using Gstarr.UI;
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

        /// <summary>
        /// 분야별 안내가 끝난 다음의 첫 전투.
        /// 테스트 몬스터는 이 방에 한 마리만 둔다.
        /// </summary>
        public const int FirstCombatRoom = 1;

        [SerializeField] Sprite doorSprite;
        [SerializeField] Sprite frameSprite;
        [SerializeField] Sprite lampSprite;
        [SerializeField] int heightInTiles = DefaultHeightTiles;

        private readonly List<ArenaRoom> rooms = new List<ArenaRoom>();
        private readonly HashSet<BossActor> configuredBosses = new HashSet<BossActor>();
        private readonly List<DoorGate> gates = new List<DoorGate>();
        private readonly List<PlacedEnemy> placedEnemies = new List<PlacedEnemy>();
        private static readonly List<Vector2> doorBarriers = new List<Vector2>();
        private static ArenaDoorSpawner active;

        private struct PlacedEnemy
        {
            public float X;
            public Vector3 Position;
            public EnemyKind Kind;
            public bool Spawned;
        }

        public static void Adopt(EnemyActor enemy)
        {
            if (active == null || enemy == null) return;
            for (int i = 0; i < active.rooms.Count; i++)
            {
                if (!active.rooms[i].Contains(enemy.transform.position.x)) continue;
                active.rooms[i].Adopt(enemy);
                return;
            }
        }

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

        /// <summary>같은 방 안에 둘 다 있는지. 문 사이 간격은 어느 방도 아니다.</summary>
        public static bool SameRoom(float a, float b)
        {
            if (active == null) return false;
            for (int i = 0; i < active.rooms.Count; i++)
            {
                var room = active.rooms[i];
                if (room.Contains(a) && room.Contains(b)) return true;
                if (room.IsLocked && room.Holds(a) && room.Holds(b)) return true;
            }
            return false;
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

        internal static void SyncBarrier(DoorGate gate)
        {
            if (gate == null || gate.BarrierIndex < 0 || gate.BarrierIndex >= doorBarriers.Count) return;
            doorBarriers[gate.BarrierIndex] = gate.FaceMoved
                ? new Vector2(gate.SolidMin, gate.SolidMax)
                : new Vector2(gate.MinX, gate.MaxX);
        }

        private Collider2D playerBody;
        private CameraSystem.CameraFollow2D cameraFollow;
        private bool corridorSettled;
        private float corridorEnter;
        private float corridorExit;
        private bool corridorGateReady;

        private void Awake()
        {
            active = this;
            var renderer = GetComponent<TilemapRenderer>();
            if (renderer != null) renderer.enabled = false;
            CollectPlacedEnemies();
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
            CollectPlacedEnemies();
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
                gate.BarrierIndex = gates.Count;
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
            if (room.HadFight) StageHUD.AnnounceRoomClear(more);
        }

        private void CollectPlacedEnemies()
        {
            placedEnemies.Clear();
            var maps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                if (map == null || map.gameObject.scene != gameObject.scene) continue;
                map.CompressBounds();
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    if (map.GetTile(cell) is not EnemyTile tile) continue;
                    Vector3 position = map.GetCellCenterWorld(cell);
                    placedEnemies.Add(new PlacedEnemy
                    {
                        X = position.x,
                        Position = position,
                        Kind = tile.Kind
                    });
                }
            }
        }

        private void OpenRoom(int index)
        {
            if (index < 0 || index >= rooms.Count) return;
            var room = rooms[index];
            if (room.IsOpened) return;
            room.MarkOpened();

            if (IsCorridorIndex(index)) return;

            bool bossRoom = index == rooms.Count - 1 && rooms.Count >= 2;
            if (bossRoom)
            {
                SpawnFeatured(room, EnemyKind.Angel, 0.55f, 24);
                return;
            }

            const float midBossX = 245f;
            if (room.FrameMin < midBossX && room.FrameMax > midBossX)
            {
                SpawnFeatured(room, EnemyKind.Shadowman, 0.5f, 16);
                return;
            }

            var plan = RoomPlan(index);
            float width = room.FrameMax - room.FrameMin;
            for (int i = 0; i < plan.Length; i++)
            {
                var actor = SpawnInto(room, plan[i], SpawnAlong(width, i, plan.Length), 0f);
                if (index == FirstCombatRoom && i == 0) actor.AllowGloryKill();
            }
        }

        /// <summary>
        /// 문 바로 안에 두지 않는다. 들어오며 걷는 동안 토끼 돌진이 닿지 않게 입구를 비운다.
        /// </summary>
        private static float SpawnAlong(float roomWidth, int index, int count)
        {
            float usable = Mathf.Max(4f, roomWidth - 2.4f);
            const float entranceClear = 11.2f;
            const float exitClear = 2.2f;
            float near = Mathf.Clamp(entranceClear, 4f, Mathf.Max(4f, roomWidth - exitClear));
            float far = roomWidth - exitClear;
            if (count > 1) far = Mathf.Max(far, near + 3.2f * (count - 1));
            far = Mathf.Min(far, Mathf.Max(near, roomWidth - 1.35f));

            float offset = count <= 1
                ? Mathf.Clamp(roomWidth * 0.68f, near, far)
                : Mathf.Lerp(near, far, index / (float)(count - 1));
            return Mathf.Clamp((offset - 1.2f) / usable, 0f, 1f);
        }

        /// <summary>앞은 토끼, 이어서 방해·추적·원거리를 한 종류씩 보여 주고, 뒤는 토끼와 섞는다. 보스 방은 여기 쓰지 않는다.</summary>
        private static EnemyKind[] RoomPlan(int index)
        {
            if (index < FirstCombatRoom) return new EnemyKind[0];

            EnemyKind[][] plans =
            {
                new[] { EnemyKind.Bunny },
                new[] { EnemyKind.Bunny, EnemyKind.Bunny },
                new[] { EnemyKind.AnnoyingKid },
                new[] { EnemyKind.Bunny, EnemyKind.DistortedBunny },
                new[] { EnemyKind.Bunny, EnemyKind.AnnoyingKid },
                new[] { EnemyKind.TwinBunny },
                new[] { EnemyKind.AnnoyingKid, EnemyKind.DistortedBunny },
                new[] { EnemyKind.BlackShadowKid },
                new[] { EnemyKind.DistortedBunny, EnemyKind.AnnoyingKid },
                new[] { EnemyKind.BlackShadowThrow },
                new[] { EnemyKind.Bunny, EnemyKind.AnnoyingKid },
                new[] { EnemyKind.TwinBunny, EnemyKind.Bunny },
                new[] { EnemyKind.AnnoyingKid, EnemyKind.TwinBunny },
                new[] { EnemyKind.DistortedBunny, EnemyKind.Bunny },
                new[] { EnemyKind.BlackShadowThrow, EnemyKind.TwinBunny },
                new[] { EnemyKind.AnnoyingKid, EnemyKind.DistortedBunny },
                new[] { EnemyKind.AnnoyingKid, EnemyKind.BlackShadowThrow },
            };
            return plans[Mathf.Clamp(index - FirstCombatRoom, 0, plans.Length - 1)];
        }

        private bool HasBossFurtherRight(float x)
        {
            for (int i = 0; i < placedEnemies.Count; i++)
                if (placedEnemies[i].Kind == EnemyKind.Boss && placedEnemies[i].X > x + 0.75f)
                    return true;
            return false;
        }

        private Tilemap groundMap;

        private void EnsureGround()
        {
            if (groundMap != null) return;
            var maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                if (maps[i] == null || maps[i].gameObject.scene != gameObject.scene) continue;
                if (maps[i].name != "Ground") continue;
                groundMap = maps[i];
                return;
            }
        }

        private void FlattenBossRoom(ArenaRoom room)
        {
            EnsureGround();
            if (groundMap == null) return;

            var sample = groundMap.WorldToCell(new Vector3(room.FrameMin + 0.4f, room.FrameY - 0.05f, 0f));
            var end = groundMap.WorldToCell(new Vector3(room.FrameMax - 0.4f, room.FrameY - 0.05f, 0f));
            int x0 = Mathf.Min(sample.x, end.x);
            int x1 = Mathf.Max(sample.x, end.x);
            int floor = sample.y;
            var bounds = groundMap.cellBounds;

            TileBase fill = null;
            for (int x = x0; x <= x1 && fill == null; x++)
            {
                for (int y = floor; y >= bounds.yMin; y--)
                {
                    fill = groundMap.GetTile(new Vector3Int(x, y, 0));
                    if (fill != null) break;
                }
            }

            for (int x = x0; x <= x1; x++)
            {
                for (int y = floor + 1; y <= floor + 8 && y < bounds.yMax; y++)
                    groundMap.SetTile(new Vector3Int(x, y, 0), null);

                if (fill == null) continue;
                bool below = false;
                for (int y = floor - 1; y >= bounds.yMin; y--)
                {
                    if (!groundMap.HasTile(new Vector3Int(x, y, 0))) continue;
                    below = true;
                    break;
                }
                groundMap.SetTile(new Vector3Int(x, floor, 0), fill);
                if (below) continue;
                for (int y = floor - 1; y >= floor - 12 && y >= bounds.yMin; y--)
                    groundMap.SetTile(new Vector3Int(x, y, 0), fill);
            }

            var maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < maps.Length; i++)
            {
                var map = maps[i];
                if (map == null || map == groundMap || map.gameObject.scene != gameObject.scene) continue;
                if (map.name.IndexOf("Platform", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                for (int x = x0; x <= x1; x++)
                    for (int y = floor; y <= floor + 8; y++)
                        map.SetTile(new Vector3Int(x, y, 0), null);
            }
        }

        private float SurfaceAt(float worldX, float fallback)
        {
            if (groundMap == null)
            {
                var maps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < maps.Length; i++)
                {
                    if (maps[i] == null || maps[i].gameObject.scene != gameObject.scene) continue;
                    if (maps[i].name != "Ground") continue;
                    groundMap = maps[i];
                    break;
                }
            }

            if (groundMap == null) return fallback;
            int x = groundMap.WorldToCell(new Vector3(worldX, fallback, 0f)).x;
            var bounds = groundMap.cellBounds;
            for (int y = bounds.yMax - 1; y >= bounds.yMin; y--)
            {
                if (!groundMap.HasTile(new Vector3Int(x, y, 0))) continue;
                return groundMap.CellToWorld(new Vector3Int(x, y + 1, 0)).y;
            }

            return fallback;
        }

        private void SpawnFeatured(ArenaRoom room, EnemyKind kind, float along, int health)
        {
            FlattenBossRoom(room);
            float x = Mathf.Lerp(room.FrameMin + 1.2f, room.FrameMax - 1.2f, along);
            float floor = SurfaceAt(x, room.FrameY);
            var position = new Vector3(x, floor + 0.08f, 0f);
            var actor = EnemyActor.Spawn(null, position, kind, health);
            room.NoteBoss(position);
            var plate = actor.gameObject.AddComponent<BossMark>();
            plate.DisplayName = kind == EnemyKind.Angel ? "석상" : "섀도우맨";
            if (kind == EnemyKind.Angel) actor.GetComponent<Angel>()?.BecomeBoss();
            else
            {
                actor.GetComponent<ShadowMan>()?.BecomeBoss();
                FoeEffects.ShadowmanArrives();
            }
            BossHealthHud.Ensure();
            actor.WaitForPlayerMove();
        }

        private EnemyActor SpawnInto(ArenaRoom room, EnemyKind kind, float along, float lift, bool midBoss = false)
        {
            if (kind == EnemyKind.BlackShadowKid || kind == EnemyKind.BlackShadowThrow || kind == EnemyKind.AnnoyingKid)
                lift = Mathf.Max(lift, 2.8f);
            if (kind == EnemyKind.Boss)
                lift = Mathf.Max(lift, 1.7f);
            float x = Mathf.Lerp(room.FrameMin + 1.2f, room.FrameMax - 1.2f, along);
            float floor = SurfaceAt(x, room.FrameY);
            var position = new Vector3(x, floor + lift + 0.08f, 0f);
            EnemyActor actor = kind == EnemyKind.Boss
                ? EnemyActor.SpawnBoss(position)
                : EnemyActor.Spawn(null, position, kind);
            if (kind == EnemyKind.Boss)
            {
                room.NoteBoss(position);
                actor.GetComponent<BossActor>()?.SetMidBoss(midBoss);
            }
            actor.WaitForPlayerMove();
            return actor;
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
            TryStartCorridor();
            AssignLeashes();
            UpdateCameraFrame(playerBody.bounds);
        }

        /// <summary>
        /// 뒤틀린 통로(배경 5·6·7)는 전투 대신 추격전이다.
        /// 카메라 원점이 없으면 Stage_1 문 위치(284~452)로 잡는다.
        /// 출구는 그 구간을 넘어가는 첫 방의 문이다. 최종 보스 방은 넘기지 않는다.
        /// </summary>
        private bool CorridorSpan(out float enter, out float exit)
        {
            const float fallbackEnter = 284f;
            const float fallbackExit = 452f;
            var background = FindAnyObjectByType<CameraSystem.ParallaxBackground>();
            if (background == null || !background.TrySegmentSpan(5, 7, out enter, out exit))
            {
                enter = fallbackEnter;
                exit = fallbackExit;
            }

            for (int i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                bool boss = i == rooms.Count - 1 && rooms.Count >= 2;
                bool startsAfter = room.FrameMin >= exit - 1f;
                bool straddles = room.FrameMax > exit && room.FrameMin < exit;
                if ((boss && room.FrameMin < exit) || startsAfter || straddles)
                {
                    if (room.FrameMin > enter + 4f)
                    {
                        exit = room.FrameMin;
                        break;
                    }
                }
            }

            return exit > enter + 8f;
        }

        /// <summary>통로 문(약 284)부터 다음 구역 문 앞까지. 마지막 보스 방은 넣지 않는다.</summary>
        private bool IsCorridorIndex(int index)
        {
            if (index < 0 || index >= rooms.Count) return false;
            if (index == rooms.Count - 1 && rooms.Count >= 2) return false;
            if (!CorridorSpan(out _, out float exit)) return false;
            var room = rooms[index];
            return room.FrameMin >= 280f && room.FrameMax <= exit + 0.45f && room.FrameMin < exit - 0.35f;
        }

        private bool TryCorridorGate(out float door, out float exit)
        {
            door = float.PositiveInfinity;
            exit = 0f;
            if (!CorridorSpan(out _, out exit)) return false;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (!IsCorridorIndex(i)) continue;
                if (rooms[i].FrameMin < door) door = rooms[i].FrameMin;
            }
            return door < exit - 4f;
        }

        private void TryStartCorridor()
        {
            if (corridorSettled || CorridorChase.IsActive || playerBody == null) return;
            if (GamePause.BlocksGameplay) return;
            if (!TryCorridorGate(out float door, out float exit)) return;
            var health = playerBody.GetComponent<Health>();
            if (health != null && health.Current <= 0) return;
            float x = playerBody.bounds.center.x;
            if (x >= exit - 0.2f)
            {
                corridorSettled = true;
                return;
            }
            if (x < door + 0.05f) return;
            var player = playerBody.GetComponent<PlayerController>();
            if (player == null) return;
            corridorEnter = door;
            corridorExit = exit;
            corridorGateReady = true;
            ClearCorridor(door, exit);
            CorridorChase.Begin(player);
        }

        private void ClearCorridor(float door, float exit)
        {
            var enemies = Object.FindObjectsByType<EnemyActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || enemy.IsBoss) continue;
                float x = enemy.transform.position.x;
                if (x < door - 0.6f || x > exit - 0.15f) continue;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }

            for (int i = 0; i < rooms.Count; i++)
                if (IsCorridorIndex(i)) rooms[i].ForceClear();
        }

        public static void ArriveAfterCorridor()
        {
            if (active == null) return;
            active.corridorSettled = true;
            active.ArriveFromCorridor();
        }

        private void ArriveFromCorridor()
        {
            if (!corridorGateReady && CorridorSpan(out float enter, out float exit))
            {
                corridorEnter = enter;
                corridorExit = exit;
            }

            int next = -1;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (IsCorridorIndex(i))
                {
                    rooms[i].ForceClear();
                    continue;
                }
                if (next < 0 && rooms[i].FrameMin >= corridorExit - 0.75f)
                    next = i;
            }

            if (next < 0 || next >= rooms.Count) return;
            var destination = rooms[next];
            float standX = destination.FrameMin + 1.7f;
            float floor = SurfaceAt(standX, destination.FrameY);

            var player = playerBody != null ? playerBody.GetComponent<PlayerController>() : null;
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.GetComponent<PlayerController>();
            }
            if (player != null)
            {
                var col = player.GetComponent<Collider2D>();
                float foot = col != null ? col.bounds.min.y - player.transform.position.y : -0.9f;
                var stand = new Vector3(standX, floor - foot + 0.05f, 0f);
                player.transform.position = stand;
                var body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.position = stand;
                    body.linearVelocity = Vector2.zero;
                }
                if (cameraFollow == null && Camera.main != null)
                    cameraFollow = Camera.main.GetComponent<CameraSystem.CameraFollow2D>();
                cameraFollow?.SetFrame(destination.FrameMin, destination.FrameMax, destination.FrameY + 2.6f);
                var cam = Camera.main;
                if (cam != null)
                {
                    var focus = new Vector3(stand.x, stand.y + 1.5f, cam.transform.position.z);
                    if (cameraFollow != null) cameraFollow.SnapTo(focus);
                    else cam.transform.position = focus;
                }
            }

            OpenRoom(next);
            FindAnyObjectByType<CameraSystem.ParallaxBackground>()?.CatchUp();
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
            for (int i = 0; i < rooms.Count; i++)
            {
                if (!rooms[i].IsLocked || !rooms[i].Holds(x)) continue;
                cameraFollow.SetFrame(rooms[i].FrameMin, rooms[i].FrameMax, rooms[i].FrameY + 2.6f);
                UpdateBossMusic(x);
                return;
            }

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
                if (rooms[i].IsLocked && rooms[i].Holds(x)) inBossRoom = true;
                else if (x >= rooms[i].FrameMin && x <= rooms[i].FrameMax) inBossRoom = true;
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
        public int BarrierIndex = -1;
        private int lockCount;
        private bool faceMoved;

        public bool FaceMoved => faceMoved;

        public float SolidMin => Blocks.Count > 0 ? Blocks[0].FaceX(false) : MinX;

        public float SolidMax => Blocks.Count > 0 ? Blocks[0].FaceX(true) : MaxX;

        public float RestInner(bool faceOnRight)
        {
            if (Blocks.Count == 0) return faceOnRight ? MaxX : MinX;
            return Blocks[0].RestFace(faceOnRight);
        }

        public float InnerFace(bool faceOnRight)
        {
            if (Blocks.Count == 0) return faceOnRight ? MaxX : MinX;
            return Blocks[0].FaceX(faceOnRight);
        }

        public void PlaceRoomFace(float worldFace, bool faceOnRight)
        {
            for (int i = 0; i < Blocks.Count; i++) Blocks[i].PlaceFace(worldFace, faceOnRight);
            faceMoved = true;
            ArenaDoorSpawner.SyncBarrier(this);
        }

        public void ResetRoomFace()
        {
            if (!faceMoved) return;
            for (int i = 0; i < Blocks.Count; i++) Blocks[i].ResetFace();
            faceMoved = false;
            ArenaDoorSpawner.SyncBarrier(this);
        }

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
                ResetRoomFace();
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

        /// <summary>통로 방은 싸움 없이 연다. 이미 잠갔으면 문도 다시 연다.</summary>
        public void ForceClear()
        {
            cleared = true;
            opened = true;
            locked = false;
            occupants.Clear();
            left.Unlock();
            right?.Unlock();
        }

        public void NoteBoss(Vector3 point)
        {
            HostsBoss = true;
            BossPoint = point;
        }

        public bool HadFight { get; private set; }
        public bool IsLocked => locked;
        public bool Contains(float x) => x >= innerMin && x <= innerMax;

        /// <summary>잠긴 동안은 화면 밖으로 밀어 둔 문 안쪽까지 이 방이다.</summary>
        public bool Holds(float x)
        {
            float min = left.InnerFace(true);
            float max = right != null ? right.InnerFace(false) : innerMax;
            return x >= min - 0.05f && x <= max + 0.05f;
        }
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

        public void Adopt(EnemyActor enemy)
        {
            if (enemy == null || occupants.Contains(enemy)) return;
            occupants.Add(enemy);
            if (cleared)
            {
                cleared = false;
                locked = true;
            }
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
            {
                AlignDoorsOutsideScreen();
                KeepInside(player, force: false);
            }

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
                if (enemy.transform.position.y < floorY - 2.2f) continue;
                occupants.Add(enemy);
            }

            if (occupants.Count == 0)
            {
                cleared = true;
                onCleared?.Invoke(this);
                return;
            }

            HadFight = true;
            locked = true;
            left.Lock();
            right?.Lock();
            AlignDoorsOutsideScreen();
            KeepInside(player, force: true);
            Physics2D.SyncTransforms();
            StageHUD.HideAdvance();
            for (int i = 0; i < occupants.Count; i++)
            {
                var boss = occupants[i] != null ? occupants[i].GetComponent<BossActor>() : null;
                if (boss != null) boss.SetEncounterActive(true);
            }
        }

        /// <summary>
        /// 시야는 그대로 두고, 방이 화면보다 좁으면 문 충돌만 화면 가장자리 밖으로 민다.
        /// </summary>
        private void AlignDoorsOutsideScreen()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                left.ResetRoomFace();
                right?.ResetRoomFace();
                return;
            }

            var follow = cam.GetComponent<CameraSystem.CameraFollow2D>();
            if (follow != null && follow.IsFocusing) return;

            float halfW = cam.orthographicSize * Mathf.Max(0.01f, cam.aspect);
            float span = innerMax - innerMin;
            if (span >= halfW * 2f - 0.02f)
            {
                left.ResetRoomFace();
                right?.ResetRoomFace();
                return;
            }

            float center = (innerMin + innerMax) * 0.5f;
            const float outside = 0.06f;
            float leftFace = center - halfW - outside;
            if (leftFace < left.RestInner(true) - 0.02f) left.PlaceRoomFace(leftFace, true);
            else left.ResetRoomFace();

            if (right == null) return;
            float rightFace = center + halfW + outside;
            if (rightFace > right.RestInner(false) + 0.02f) right.PlaceRoomFace(rightFace, false);
            else right.ResetRoomFace();
        }

        private void KeepInside(Collider2D player, bool force)
        {
            float half = player.bounds.extents.x;
            float leftEdge = left.InnerFace(true);
            float rightEdge = right != null ? right.InnerFace(false) : float.PositiveInfinity;
            float min = leftEdge + half + 0.02f;
            float max = rightEdge - half - 0.02f;
            if (min > max) min = max = (innerMin + innerMax) * 0.5f;

            var body = player.attachedRigidbody;
            float x = body != null ? body.position.x : player.transform.position.x;
            // 문에 붙어 비비는 것은 그대로 둔다. 문을 통과했을 때만 끌어온다.
            if (!force && x >= min - 0.6f && x <= max + 0.6f) return;

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

            // 모양은 그리지 않는다. 발 높이보다 위에서만 막아 바닥 모서리에 걸리지 않게 한다.
            var box = go.AddComponent<BoxCollider2D>();
            const float clearance = 0.55f;
            float wallHeight = Mathf.Max(0.2f, frameH - clearance);
            box.size = new Vector2(Mathf.Max(0.45f, targetW * 0.72f), wallHeight);
            box.offset = new Vector2(0f, clearance + wallHeight * 0.5f);
            box.sharedMaterial = new PhysicsMaterial2D("DoorSlip")
            {
                friction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine2D.Minimum,
                bounceCombine = PhysicsMaterialCombine2D.Minimum
            };
            box.enabled = false;
            block.body = box;
            block.restHalf = box.size.x * 0.5f;
            block.restCenterX = go.transform.position.x;
            return block;
        }

        private float restCenterX;
        private float restHalf;

        public float RestFace(bool faceOnRight) => faceOnRight ? restCenterX + restHalf : restCenterX - restHalf;

        public float FaceX(bool faceOnRight)
        {
            if (body is not BoxCollider2D box) return RestFace(faceOnRight);
            float half = box.size.x * 0.5f;
            float center = transform.position.x + box.offset.x;
            return faceOnRight ? center + half : center - half;
        }

        public void PlaceFace(float worldFace, bool faceOnRight)
        {
            if (body is not BoxCollider2D box) return;
            float half = box.size.x * 0.5f;
            float center = faceOnRight ? worldFace - half : worldFace + half;
            var offset = box.offset;
            offset.x = center - transform.position.x;
            box.offset = offset;
        }

        public void ResetFace()
        {
            if (body is not BoxCollider2D box) return;
            var offset = box.offset;
            offset.x = 0f;
            box.offset = offset;
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
