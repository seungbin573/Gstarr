using System.Collections.Generic;
using System.IO;
using Gstarr.Items;
using UnityEngine;

namespace Gstarr.Combat
{
    /// <summary>빌드에 포함된 스프라이트를 공통 높이와 발밑 피벗으로 재생한다.</summary>
    public static class CharacterArt
    {
        private const float BodyHeight = 2.2f;

        public static bool Owns(EnemyKind kind) => kind >= EnemyKind.Angel;

        public static Sprite Idle(EnemyKind kind) => First(Frames(kind, Pose.Idle));

        /// <summary>준비 동작의 앞 프레임. 트윈과 디스토티드만 있다.</summary>
        public static Sprite WindupLead(EnemyKind kind)
        {
            if (kind == EnemyKind.TwinBunny) return First(LoadVisible(TwinAlert, TwinIdle[0]));
            if (kind == EnemyKind.DistortedBunny) return First(LoadVisible(DistortedAlert, DistortedIdle[0]));
            return null;
        }

        public static Sprite[] Frames(EnemyKind kind, Pose pose)
        {
            return kind switch
            {
                EnemyKind.Angel => LoadVisible(pose == Pose.Charge ? AngelCharge : AngelIdle, AngelIdle[0]),
                EnemyKind.AnnoyingKid => pose switch
                {
                    Pose.Throw => LoadVisible(KidThrow, KidIdle[0], 0.68f),
                    Pose.Scare => Load(KidScare),
                    Pose.Shot => Load(KidBall, 0.72f),
                    Pose.Laugh => LoadVisible(KidLaugh, KidIdle[0], 0.68f),
                    _ => LoadVisible(KidIdle, KidIdle[0], 0.68f)
                },
                EnemyKind.BlackShadowKid => LoadVisible(pose == Pose.Charge ? ShadowCharge : ShadowIdle, ShadowIdle[0]),
                EnemyKind.BlackShadowThrow => pose switch
                {
                    Pose.Throw => LoadVisible(ShadowThrow, ShadowThrowIdle[0]),
                    Pose.Shot => Load(ShadowPaper, 0.55f),
                    _ => LoadVisible(ShadowThrowIdle, ShadowThrowIdle[0])
                },
                EnemyKind.Bunny => LoadVisible(pose == Pose.Charge ? BunnyCharge : BunnyIdle, BunnyIdle[0]),
                EnemyKind.DistortedBunny => LoadVisible(pose == Pose.Charge ? DistortedCharge : DistortedIdle, DistortedIdle[0]),
                EnemyKind.TwinBunny => LoadVisible(pose == Pose.Charge ? TwinCharge : TwinIdle, TwinIdle[0]),
                EnemyKind.Shadowman => pose == Pose.Grab
                    ? LoadVisible(ManGrab, ManWalk[0], 1.32f)
                    : LoadVisible(ManWalk, ManWalk[0], 1.32f),
                _ => System.Array.Empty<Sprite>()
            };
        }

        public enum Pose
        {
            Idle,
            Charge,
            Throw,
            Laugh,
            Vanish,
            Grab,
            Scare,
            Shot
        }

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static Sprite First(Sprite[] frames)
        {
            if (frames == null) return null;
            for (int i = 0; i < frames.Length; i++)
                if (frames[i] != null) return frames[i];
            return null;
        }

        private static Sprite[] Join(params Sprite[][] parts)
        {
            var frames = new List<Sprite>();
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                for (int j = 0; j < parts[i].Length; j++)
                    if (parts[i][j] != null) frames.Add(parts[i][j]);
            }
            return frames.ToArray();
        }

        private static Sprite[] Load(string[] paths, float height = BodyHeight)
        {
            var frames = new List<Sprite>(paths.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                var sprite = LoadOne(paths[i], height, 0f);
                if (sprite != null) frames.Add(sprite);
            }
            return frames.ToArray();
        }

        /// <summary>
        /// idle에서 보이는 영역의 면적에 맞춘다. 가로로 긴 돌진을 키만 맞춰 키우지 않고, 발은 가장 아래 픽셀에 둔다.
        /// </summary>
        private static Sprite[] LoadVisible(string[] paths, string referencePath, float scale = 1f)
        {
            var reference = MeasureVisible(referencePath);
            float sized = BodyHeight * Mathf.Max(0.25f, scale);
            float fallback = ReferencePpu(referencePath) / Mathf.Max(0.25f, scale);
            float worldBulk = 0f;
            if (reference.Width > 0 && reference.Height > 0 && reference.CanvasHeight > 0)
            {
                float worldHeight = sized * reference.Height / reference.CanvasHeight;
                float worldWidth = worldHeight * reference.Width / reference.Height;
                worldBulk = Mathf.Sqrt(worldWidth * worldHeight);
            }

            var frames = new List<Sprite>(paths.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                var body = MeasureVisible(paths[i]);
                float ppu = body.Width > 0 && body.Height > 0 && worldBulk > 0.05f
                    ? Mathf.Sqrt(body.Width * (float)body.Height) / worldBulk
                    : fallback;
                var sprite = LoadAnchored(paths[i], ppu);
                if (sprite != null) frames.Add(sprite);
            }
            return frames.ToArray();
        }

        private struct VisibleSpan
        {
            public int Width;
            public int Height;
            public int CanvasHeight;
        }

        private static VisibleSpan MeasureVisible(string assetPath)
        {
            var texture = ReadTexture(assetPath);
            if (texture == null) return default;
            var pixels = texture.GetPixels32();
            int w = texture.width;
            int h = texture.height;
            Object.Destroy(texture);
            var span = new VisibleSpan { CanvasHeight = h };
            if (w < 2 || h < 2 || pixels == null || pixels.Length < w * h) return span;

            int minX = w;
            int minY = h;
            int maxX = -1;
            int maxY = -1;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= 40) continue;
                int x = i % w;
                int y = i / w;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            if (maxX < minX || maxY < minY) return span;
            span.Width = maxX - minX + 1;
            span.Height = maxY - minY + 1;
            return span;
        }

        private static float ReferencePpu(string referencePath)
        {
            int referenceHeight = PngPixelHeight(referencePath);
            if (referenceHeight <= 0)
            {
                var reference = FindInAssets(referencePath);
                if (reference != null) referenceHeight = Mathf.RoundToInt(reference.rect.height);
            }
            return referenceHeight > 0 ? referenceHeight / BodyHeight : 100f;
        }

        private static Sprite LoadAnchored(string assetPath, float ppu)
        {
            if (string.IsNullOrEmpty(assetPath) || ppu <= 0f) return null;
            string key = assetPath + "#anchor@" + ppu.ToString("0.###");
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = ReadTexture(assetPath);
            if (texture == null) return null;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                BodyPivot(texture),
                ppu,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>몸통 가운데를 가로 피벗으로, 가장 아래 픽셀을 발밑 피벗으로 둔다.</summary>
        private static Vector2 BodyPivot(Texture2D texture)
        {
            var pixels = texture.GetPixels32();
            int w = texture.width;
            int h = texture.height;
            if (w < 2 || h < 2 || pixels == null || pixels.Length < w * h)
                return new Vector2(0.5f, 0f);

            int maxThick = 0;
            int foot = h;
            var thick = new int[w];
            for (int x = 0; x < w; x++)
            {
                int bottom = -1;
                int top = -1;
                for (int y = 0; y < h; y++)
                {
                    if (pixels[x + y * w].a <= 40) continue;
                    if (bottom < 0) bottom = y;
                    top = y;
                    if (y < foot) foot = y;
                }

                thick[x] = bottom < 0 ? 0 : top - bottom + 1;
                if (thick[x] > maxThick) maxThick = thick[x];
            }

            if (maxThick < 8 || foot >= h) return new Vector2(0.5f, 0f);
            int minX = w;
            int maxX = -1;
            int limit = Mathf.CeilToInt(maxThick * 0.55f);
            for (int x = 0; x < w; x++)
            {
                if (thick[x] < limit) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }

            float pivotX = maxX < minX ? 0.5f : (minX + maxX) * 0.5f / w;
            return new Vector2(pivotX, foot / (float)h);
        }

        private static Texture2D ReadTexture(string assetPath)
        {
            string full = FullPath(assetPath);
            if (!File.Exists(full)) return null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(full); }
            catch { return null; }
            if (bytes == null || bytes.Length < 8) return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = Path.GetFileNameWithoutExtension(assetPath);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (texture.LoadImage(bytes, false)) return texture;
            Object.Destroy(texture);
            return null;
        }

        private static Sprite LoadOne(string path, float height, float fixedPpu)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key = fixedPpu > 0f
                ? path + "#ppu" + fixedPpu.ToString("0.###")
                : path + "@" + height.ToString("0.###");
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            Sprite sprite = LoadFromPng(path, height, fixedPpu);
            if (sprite == null)
            {
                Sprite source = FindInAssets(path);
#if UNITY_EDITOR
                if (source == null) source = LoadEditorSprite(path);
#endif
                if (source == null) return null;
                float ppu = fixedPpu > 0f ? fixedPpu : source.rect.height / height;
                sprite = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0f),
                    ppu, 0, SpriteMeshType.FullRect);
                sprite.name = source.name;
            }

            Cache[key] = sprite;
            return sprite;
        }

        private static Sprite LoadFromPng(string assetPath, float height, float fixedPpu)
        {
            string full = FullPath(assetPath);
            if (!File.Exists(full)) return null;

            byte[] bytes;
            try { bytes = File.ReadAllBytes(full); }
            catch { return null; }
            if (bytes == null || bytes.Length < 8) return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = Path.GetFileNameWithoutExtension(assetPath);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (!texture.LoadImage(bytes, false))
            {
                Object.Destroy(texture);
                return null;
            }

            float ppu = fixedPpu > 0f
                ? fixedPpu
                : Mathf.Max(1f, texture.height / Mathf.Max(0.2f, height));
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0f),
                ppu,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

#if UNITY_EDITOR
        private static Sprite LoadEditorSprite(string path)
        {
            var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                    if (all[i] is Sprite sprite) return sprite;
            }
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
#endif

        private static string FullPath(string assetPath)
        {
            string relative = assetPath.Replace('\\', '/');
            if (relative.StartsWith("Assets/")) relative = relative.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
        }

        private static int PngPixelHeight(string assetPath)
        {
            string full = FullPath(assetPath);
            if (!File.Exists(full)) return 0;
            try
            {
                using var stream = File.OpenRead(full);
                var header = new byte[24];
                if (stream.Read(header, 0, header.Length) < header.Length) return 0;
                return (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            }
            catch
            {
                return 0;
            }
        }

        private static Sprite FindInAssets(string path)
        {
            var assets = GameAssets.Instance;
            if (assets == null) return null;
            string file = Path.GetFileNameWithoutExtension(path);
            return Match(assets.angelSprites, file)
                ?? Match(assets.annoyingKidSprites, file)
                ?? Match(assets.blackKidSprites, file)
                ?? Match(assets.blackKidThrowSprites, file)
                ?? Match(assets.bunnySprites, file)
                ?? Match(assets.distortedBunnySprites, file)
                ?? Match(assets.twinBunnySprites, file)
                ?? Match(assets.shadowmanSprites, file);
        }

        private static Sprite Match(Sprite[] list, string file)
        {
            if (list == null || string.IsNullOrEmpty(file)) return null;
            string want = Normalize(file);
            for (int i = 0; i < list.Length; i++)
            {
                var sprite = list[i];
                if (sprite == null) continue;
                string have = Normalize(sprite.name);
                if (have == want || have.StartsWith(want) || want.StartsWith(have))
                    return sprite;
            }
            return null;
        }

        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var chars = new char[value.Length];
            int n = 0;
            bool space = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = char.ToLowerInvariant(value[i]);
                if (c == '_' && i + 1 < value.Length && value[i + 1] == '0' && i + 2 == value.Length)
                    break;
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (space && n > 0) chars[n++] = ' ';
                space = false;
                chars[n++] = c;
            }
            return new string(chars, 0, n);
        }

        private static readonly string[] AngelIdle =
        {
            "Assets/arts/character/angel/the angel state idle.png"
        };

        private static readonly string[] AngelCharge =
        {
            "Assets/arts/character/angel/the angel state raises her head.png",
            "Assets/arts/character/angel/the angel state stretches her hand.png",
            "Assets/arts/character/angel/the angel state stretches her hand to summon.png"
        };

        private static readonly string[] KidIdle =
        {
            "Assets/arts/character/ankid/the annoying kid idle.png",
            "Assets/arts/character/ankid/the annoying kid idle 1.png",
            "Assets/arts/character/ankid/the annoying kid idle 2.png",
            "Assets/arts/character/ankid/the annoying kid idle 3.png"
        };

        private static readonly string[] KidThrow =
        {
            "Assets/arts/character/ankid/the annoying kid throwing a ball.png",
            "Assets/arts/character/ankid/the annoying kid throwing a ball 1.png"
        };

        private static readonly string[] KidLaugh =
        {
            "Assets/arts/character/ankid/the annoying kid laughing.png"
        };

        private static readonly string[] KidBall =
        {
            "Assets/arts/character/ankid/the annoying kid's ball.png",
            "Assets/arts/character/ankid/the annoying kid's ball.  2.png"
        };

        private static readonly string[] KidScare =
        {
            "Assets/arts/character/ankid/the annoying kid's distraction.png",
            "Assets/arts/character/ankid/the annoying kid's distraction2.png"
        };

        private static readonly string[] ShadowIdle =
        {
            "Assets/arts/character/blackKid/black shadow kid entity 1.png"
        };

        private static readonly string[] ShadowCharge =
        {
            "Assets/arts/character/blackKid/black shadow kid entity 1 about to charge.png",
            "Assets/arts/character/blackKid/black shadow kid entity 1 about to charge 22.png",
            "Assets/arts/character/blackKid/black shadow kid entity 1 about to charge 33.png",
            "Assets/arts/character/blackKid/black shadow kid entity 1 about to charge 44.png"
        };

        private static readonly string[] ShadowThrowIdle =
        {
            "Assets/arts/character/blackKidThrow/black shadow kid entity grabbing rolled paper.png"
        };

        private static readonly string[] ShadowThrow =
        {
            "Assets/arts/character/blackKidThrow/black shadow kid entity about to throw.png",
            "Assets/arts/character/blackKidThrow/black shadow kid entity about to throw2.png",
            "Assets/arts/character/blackKidThrow/black shadow kid entity about to throw3.png",
            "Assets/arts/character/blackKidThrow/black shadow kid entity about to threw.png"
        };

        private static readonly string[] ShadowPaper =
        {
            "Assets/arts/character/blackKidThrow/the rolled paper thrown by black shadow kid entity.png"
        };

        private static readonly string[] BunnyIdle =
        {
            "Assets/arts/character/bunny/the normal bunny 1.png"
        };

        private static readonly string[] BunnyCharge =
        {
            "Assets/arts/character/bunny/the normal bunny 2.png",
            "Assets/arts/character/bunny/the normal bunny 3png.png",
            "Assets/arts/character/bunny/the normal bunny 4png.png"
        };

        private static readonly string[] DistortedIdle =
        {
            "Assets/arts/character/d_bunny/the distorted bunny.png"
        };

        private static readonly string[] DistortedAlert =
        {
            "Assets/arts/character/d_bunny/the distorted bunny1.png"
        };

        private static readonly string[] DistortedCharge =
        {
            "Assets/arts/character/d_bunny/the distorted bunny2.png",
            "Assets/arts/character/d_bunny/the distorted bunny3.png",
            "Assets/arts/character/d_bunny/the distorted bunny4 .png"
        };

        private static readonly string[] TwinIdle =
        {
            "Assets/arts/character/twinBunny/the twin bunny.png"
        };

        private static readonly string[] TwinAlert =
        {
            "Assets/arts/character/twinBunny/the twin bunny 1 .png"
        };

        private static readonly string[] TwinCharge =
        {
            "Assets/arts/character/twinBunny/the twin bunny 2 .png",
            "Assets/arts/character/twinBunny/the twin bunny 3 .png",
            "Assets/arts/character/twinBunny/the twin bunny4 .png"
        };

        private static readonly string[] ManWalk =
        {
            "Assets/arts/character/shadowman/the kitty room shadow man walking png.png",
            "Assets/arts/character/shadowman/the kitty room shadow man walking 1 png.png",
            "Assets/arts/character/shadowman/the kitty room shadow man walking 2 png.png",
            "Assets/arts/character/shadowman/the kitty room shadow man walking 3 png.png"
        };

        private static readonly string[] ManVanish =
        {
            "Assets/arts/character/shadowman/the kitty room shadow man disappearing.png",
            "Assets/arts/character/shadowman/the kitty room shadow man disappearing to teleport.png",
            "Assets/arts/character/shadowman/the kitty room shadow man disappearing to teleport1.png"
        };

        private static readonly string[] ManGrab =
        {
            "Assets/arts/character/shadowman/the kitty room shadow man reaching out .png",
            "Assets/arts/character/shadowman/the kitty room shadow man reaching out 1 .png",
            "Assets/arts/character/shadowman/the kitty room shadow man reaching out 2 .png"
        };
    }
}
