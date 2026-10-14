#!/usr/bin/env python3
"""
Descarga todos los emojis de JoyPixels 5.0 (2019-06-20, soporta Emoji 12.0)
desde el CDN de Emojipedia y los guarda en:
    src/BitLifeClone/Assets/Images/emoji/joypixels-5/

Tambien genera `index.json` (slug -> archivo, categoria, codepoints) para que
el juego pueda usarlos como iconos.

Uso:
    python tools/download_joypixels.py            # descarga / reanuda
    python tools/download_joypixels.py --list     # solo (re)genera la lista

Licencia: JoyPixels es una fuente con licencia. El plan gratuito exige
atribucion ("Emoji provided by JoyPixels (joypixels.com)") en la app.
Ver NOTICE.txt generado junto a las imagenes.
"""
import json
import os
import sys
import time
import urllib.request
from concurrent.futures import ThreadPoolExecutor, as_completed

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "src", "BitLifeClone", "Assets", "Images", "emoji", "joypixels-5")
LIST_CACHE = os.path.join(ROOT, "tools", "joypixels-5-list.json")
CDN = "https://em-content.zobj.net/"
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) BitLifeClone-asset-fetch"

GRAPHQL_QUERY = """
query vendorHistoricEmojiV1($slug: Slug!, $version: Slug = null, $status: VendorHistoricEmojiStatus = null, $lang: Language) {
  vendorHistoricEmoji_v1(slug: $slug, version: $version, status: $status, lang: $lang) {
    ...vendorHistoricEmojiResource
  }
}
fragment vendorHistoricEmojiImageFragment on VendorHistoricEmojiImage {
  slug
  image { source description useOriginalImage isMonochrome }
  status
}
fragment vendorHistoricEmojiResource on VendorHistoricEmoji {
  items {
    category { slug title representingEmoji { code } }
    images { ...vendorHistoricEmojiImageFragment }
  }
  statuses
}
"""

NOTICE = """Emoji images: JoyPixels (R) 5.0
Source: https://emojipedia.org/joypixels/5.0 (via Emojipedia CDN)

JoyPixels is a licensed emoji set (formerly EmojiOne).
If you distribute this app under the JoyPixels FREE license you MUST include
an attribution such as: "Emoji provided by JoyPixels (joypixels.com)".
See https://www.joypixels.com/license for full terms.
"""


def fetch_list():
    """Consulta la API GraphQL de Emojipedia y devuelve [(slug, source, category, status)]."""
    payload = {
        "query": GRAPHQL_QUERY,
        "operationName": "vendorHistoricEmojiV1",
        "variables": {"slug": "joypixels", "version": "5.0", "lang": "EN"},
    }
    req = urllib.request.Request(
        "https://emojipedia.org/api/graphql",
        data=json.dumps(payload).encode(),
        headers={
            "Content-Type": "application/json",
            "User-Agent": UA,
            "Origin": "https://emojipedia.org",
        },
        method="POST",
    )
    data = json.loads(urllib.request.urlopen(req, timeout=60).read())
    if data.get("errors"):
        raise RuntimeError(f"GraphQL errors: {data['errors']}")

    entries = []
    for item in data["data"]["vendorHistoricEmoji_v1"]["items"]:
        cat = item["category"]["slug"]
        for im in item["images"]:
            img = im.get("image")
            if not img or not img.get("source"):
                continue
            entries.append(
                {"slug": im["slug"], "source": img["source"], "category": cat, "status": im.get("status")}
            )
    with open(LIST_CACHE, "w", encoding="utf-8") as f:
        json.dump(entries, f, ensure_ascii=False, indent=1)
    return entries


def load_list(refresh=False):
    if refresh or not os.path.exists(LIST_CACHE):
        print("Consultando lista JoyPixels 5.0 a Emojipedia...")
        return fetch_list()
    with open(LIST_CACHE, encoding="utf-8") as f:
        return json.load(f)


def valid_png(path):
    try:
        if os.path.getsize(path) < 100:
            return False
        with open(path, "rb") as f:
            return f.read(8) == b"\x89PNG\r\n\x1a\n"
    except OSError:
        return False


def download(entry, dest):
    url = CDN + entry["source"]
    fname = entry["source"].rsplit("/", 1)[-1]
    path = os.path.join(dest, fname)
    if valid_png(path):
        return fname, "cached", None
    last_err = None
    for attempt in range(3):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=30) as r:
                blob = r.read()
            if not blob.startswith(b"\x89PNG"):
                raise ValueError("respuesta no-PNG")
            with open(path, "wb") as f:
                f.write(blob)
            return fname, "ok", None
        except Exception as e:  # noqa: BLE001 - reintento sobre cualquier error de red
            last_err = e
            time.sleep(0.5 * (attempt + 1))
    try:
        if os.path.exists(path):
            os.remove(path)
    except OSError:
        pass
    return fname, "fail", str(last_err)


def main():
    refresh = "--list" in sys.argv
    entries = load_list(refresh=refresh)
    print(f"Emoji en la lista: {len(entries)}")
    if refresh:
        print("Solo lista generada (--list).")
        return 0

    os.makedirs(OUT_DIR, exist_ok=True)
    with open(os.path.join(OUT_DIR, "NOTICE.txt"), "w", encoding="utf-8") as f:
        f.write(NOTICE)

    # index.json para el juego
    index = {}
    for e in entries:
        fname = e["source"].rsplit("/", 1)[-1]
        codepoints = fname.rsplit("_", 1)[-1].removesuffix(".png")
        index[e["slug"]] = {
            "file": fname,
            "category": e["category"],
            "codepoints": codepoints,
            "status": e["status"],
        }
    with open(os.path.join(OUT_DIR, "index.json"), "w", encoding="utf-8") as f:
        json.dump(index, f, ensure_ascii=False, indent=1, sort_keys=True)

    ok = cached = failed = 0
    failures = []
    t0 = time.time()
    with ThreadPoolExecutor(max_workers=8) as pool:
        futures = [pool.submit(download, e, OUT_DIR) for e in entries]
        for i, fut in enumerate(as_completed(futures), 1):
            fname, state, err = fut.result()
            if state == "ok":
                ok += 1
            elif state == "cached":
                cached += 1
            else:
                failed += 1
                failures.append((fname, err))
            if i % 500 == 0:
                print(f"  {i}/{len(entries)}... (ok={ok} cache={cached} fail={failed})")

    print(f"Terminado en {time.time() - t0:.1f}s: ok={ok} cache={cached} fail={failed}")
    if failures:
        print("FALLOS:")
        for fname, err in failures[:20]:
            print(f"  {fname}: {err}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
