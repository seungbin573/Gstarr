"""Pack Assets/arts/UI/tutorial/*.gif into Resources atlases the lesson UI can play."""
import os
import uuid
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
SRC = os.path.join(ROOT, "Assets", "arts", "UI", "tutorial")
OUT = os.path.join(ROOT, "Assets", "_Project", "Resources", "TutorialGifs")
MAX_ATLAS = 4096
PAD = 2
NAMES = ("moving", "jump", "dash", "itemChange", "itemUse", "glorykill", "weaponAttack", "weaponChange")


def choose(count, src_w, src_h):
    aspect = src_h / float(src_w)
    for cell_w in range(640, 160, -8):
        cell_h = max(1, int(round(cell_w * aspect)))
        stride_x = cell_w + PAD * 2
        stride_y = cell_h + PAD * 2
        cols = MAX_ATLAS // stride_x
        if cols < 1:
            continue
        rows = (count + cols - 1) // cols
        width = cols * stride_x
        height = rows * stride_y
        if height <= MAX_ATLAS:
            return cell_w, cell_h, cols, rows, width, height
    raise RuntimeError("frame does not fit")


def solid(color, w, h):
    return Image.new("RGB", (w, h), color)


def extrude(frame):
    w, h = frame.size
    canvas = Image.new("RGB", (w + PAD * 2, h + PAD * 2))
    canvas.paste(frame, (PAD, PAD))
    canvas.paste(frame.crop((0, 0, w, 1)).resize((w, PAD), Image.Resampling.NEAREST), (PAD, 0))
    canvas.paste(frame.crop((0, h - 1, w, h)).resize((w, PAD), Image.Resampling.NEAREST), (PAD, PAD + h))
    canvas.paste(frame.crop((0, 0, 1, h)).resize((PAD, h), Image.Resampling.NEAREST), (0, PAD))
    canvas.paste(frame.crop((w - 1, 0, w, h)).resize((PAD, h), Image.Resampling.NEAREST), (PAD + w, PAD))
    canvas.paste(solid(frame.getpixel((0, 0)), PAD, PAD), (0, 0))
    canvas.paste(solid(frame.getpixel((w - 1, 0)), PAD, PAD), (PAD + w, 0))
    canvas.paste(solid(frame.getpixel((0, h - 1)), PAD, PAD), (0, PAD + h))
    canvas.paste(solid(frame.getpixel((w - 1, h - 1)), PAD, PAD), (PAD + w, PAD + h))
    return canvas


def write_meta(path, width, height):
    guid = uuid.uuid4().hex
    sprite_id = uuid.uuid4().hex
    meta = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 8192
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 100
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 1
  compressionQualitySet: 1
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 8192
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 2
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 8192
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 2
    compressionQuality: 100
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites:
    - serializedVersion: 2
      name: {name}
      rect:
        serializedVersion: 2
        x: 0
        y: 0
        width: {width}
        height: {height}
      alignment: 0
      pivot: {{x: 0.5, y: 0.5}}
      border: {{x: 0, y: 0, z: 0, w: 0}}
      outline: []
      physicsShape: []
      tessellationDetail: -1
      bones: []
      spriteID: {sprite_id}
      internalID: 21300000
      vertices: []
      indices: 
      edges: []
      weights: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices: 
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName: 
  pSDRemoveMatte: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""".format(guid=guid, name=os.path.splitext(os.path.basename(path))[0], width=width, height=height, sprite_id=sprite_id)
    with open(path + ".meta", "w", newline="\n") as handle:
        handle.write(meta)


def bake(name):
    path = os.path.join(SRC, name + ".gif")
    im = Image.open(path)
    count = im.n_frames
    cell_w, cell_h, cols, rows, width, height = choose(count, im.size[0], im.size[1])
    sheet = Image.new("RGB", (width, height), (0, 0, 0))
    delays = []
    stride_x = cell_w + PAD * 2
    stride_y = cell_h + PAD * 2
    for index in range(count):
        im.seek(index)
        delays.append(int(im.info.get("duration", 40) or 40))
        frame = im.convert("RGB").resize((cell_w, cell_h), Image.Resampling.BILINEAR)
        col = index % cols
        row = index // cols
        sheet.paste(extrude(frame), (col * stride_x, row * stride_y))
        if index % 20 == 0:
            print(name, index, "/", count)
    png = os.path.join(OUT, name + ".png")
    sheet.save(png, "PNG", optimize=False, compress_level=3)
    write_meta(png, width, height)
    info = os.path.join(OUT, name + "_frames.txt")
    with open(info, "w", newline="\n") as handle:
        handle.write("{0} {1} {2} {3} {4} {5} {6}\n".format(count, cols, cell_w, cell_h, PAD, width, height))
        handle.write(",".join(str(d) for d in delays))
        handle.write("\n")
    print("wrote", name, sheet.size, "frames", count, "cell", cell_w, cell_h)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name in NAMES:
        bake(name)


if __name__ == "__main__":
    main()
