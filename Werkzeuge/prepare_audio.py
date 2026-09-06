"""Reproduzierbare Audio-Importe. Aufruf: python prepare_audio.py DOWNLOAD_ORDNER
Benötigt numpy und soundfile. Quellenarchive bleiben außerhalb von Assets.
"""
import hashlib
import io
import json
from pathlib import Path
import shutil
import sys
import zipfile

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[1]
DOWNLOADS = Path(sys.argv[1]).resolve()
PROOF = ROOT / 'Dokumentation/Licenses/Audio'
PROOF.mkdir(parents=True, exist_ok=True)
manifest_path = ROOT / 'Assets/_Project/Resources/asset-credits.json'
manifest = json.loads(manifest_path.read_text())
entries = {entry['id']: entry for entry in manifest['entries']}
stats = []


def digest(data):
    return hashlib.sha256(data).hexdigest()


def package(key, name, author, license_name, source, download, license_text=None,
            license_url='https://creativecommons.org/publicdomain/zero/1.0/', note=''):
    folder = ROOT / 'Assets/ThirdParty/Audio' / key
    folder.mkdir(parents=True, exist_ok=True)
    evidence = {
        'sourceUrl': source, 'downloadFile': download,
        'downloadSha256': digest((DOWNLOADS / download).read_bytes()),
        'author': author, 'license': license_name, 'licenseUrl': license_url,
        'inspection': ('Licence inside the package retained verbatim.' if license_text else
                       'No licence file inside this download. Licence and author checked on the source page; this is provider evidence, not a package licence.'),
    }
    proof = PROOF / (key + '.json')
    proof.write_text(json.dumps(evidence, indent=2) + '\n')
    evidence_files = [{'path': str(proof.relative_to(ROOT)), 'kind': 'download-and-provider-evidence',
                       'sourceUrl': source, 'sha256': digest(proof.read_bytes())}]
    if license_text:
        license_file = folder / 'LICENSE.txt'
        license_file.write_bytes(license_text)
        evidence_files.append({'path': str(license_file.relative_to(ROOT)), 'kind': 'package-license',
                               'sourceUrl': source, 'sha256': digest(license_text)})
    entry = {'id': key, 'name': name, 'author': author, 'license': license_name,
             'licenseUrl': license_url, 'attributionNote': note, 'sourceUrl': source,
             'provenanceStatus': 'package-license-verified' if license_text else 'provider-license-verified',
             'notes': evidence['inspection'], 'optional': False,
             'paths': [str(folder.relative_to(ROOT))], 'evidence': evidence_files, 'content': []}
    entries[key] = entry
    return folder, entry


def write_clip(pkg, name, data, sr, peak=.7, loop=False):
    folder, entry = pkg
    if data.ndim > 1 and not name.startswith('musik_'):
        data = data.mean(axis=1)
    data = data.astype(np.float64)
    data -= data.mean(axis=0)
    if sr == 96000:
        spectrum = np.fft.rfft(data, axis=0)
        spectrum[np.fft.rfftfreq(len(data), 1 / sr) > 20000] = 0
        data = np.fft.irfft(spectrum, n=len(data), axis=0)[::2]
        sr = 48000
    if loop:
        n = min(int(sr * .5), len(data) // 8)
        fade = np.linspace(0, 1, n)
        if data.ndim == 2:
            fade = fade[:, None]
        data[:n] = data[-n:] * (1 - fade) + data[:n] * fade
        data = data[:-n]
    else:
        n = min(int(sr * .006), len(data) // 8)
        fade = np.linspace(0, 1, n)
        if data.ndim == 2:
            fade = fade[:, None]
        data[:n] *= fade
        data[-n:] *= fade[::-1]
    data *= peak / max(float(np.abs(data).max()), 1e-8)
    relative = Path('Resources/InfrontAudio') / (name + '.wav')
    path = folder / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    sf.write(path, data, sr, subtype='PCM_16')
    entry['content'].append({'path': str(path.relative_to(ROOT)), 'sha256': digest(path.read_bytes())})
    stats.append({'resource': 'InfrontAudio/' + name, 'seconds': len(data) / sr,
                  'rate': sr, 'peak': float(np.abs(data).max()), 'rms': float(np.sqrt(np.mean(data * data))),
                  'credit': entry['id']})
    return 'InfrontAudio/' + name


clips = {}
z = zipfile.ZipFile(DOWNLOADS / 'footsteps.zip')
for surface, author, title, source, license_name in [
    ('boots', 'swuing; mastered by congusbongus', 'footstep-concrete.wav', 'https://freesound.org/people/swuing/sounds/38873/', 'CC-BY-3.0'),
    ('metal', 'Eelke; mastered by congusbongus', 'fboots on aluminum ladder 01', 'https://freesound.org/people/Eelke/sounds/462598/', 'CC-BY-3.0'),
    ('gravel', 'Ali_6868; mastered by congusbongus', 'Gravel Footsteps', 'https://freesound.org/people/Ali_6868/packs/21608/', 'CC0-1.0'),
]:
    pkg = package('footsteps-' + surface, title, author, license_name, source, 'footsteps.zip',
                  z.read('footsteps/' + surface + '/license.txt'),
                  'https://creativecommons.org/licenses/by/3.0/' if license_name == 'CC-BY-3.0' else 'https://creativecommons.org/publicdomain/zero/1.0/',
                  'Extracted from Footsteps on different surfaces by congusbongus. Mono conversion, level adjustment and short edge fades by Driftlab.')
    clips[surface] = []
    for name in sorted(n for n in z.namelist() if n.startswith('footsteps/' + surface + '/') and n.endswith('.ogg')):
        data, sr = sf.read(io.BytesIO(z.read(name)))
        clips[surface].append(write_clip(pkg, 'schritt_' + surface + '_' + Path(name).stem, data, sr, .55))

z = zipfile.ZipFile(DOWNLOADS / 'kenney-impact.zip')
pkg = package('kenney-impact-sounds', 'Impact Sounds 1.0', 'Kenney', 'CC0-1.0',
              'https://kenney.nl/assets/impact-sounds', 'kenney-impact.zip', z.read('License.txt'),
              note='Selected impact foley; mono conversion, level adjustment and edge fades by Driftlab.')
for group, prefix in [('wall', 'impactMining'), ('metal-hit', 'impactMetal_light'), ('body', 'impactPunch_medium')]:
    clips[group] = []
    for i in range(5):
        data, sr = sf.read(io.BytesIO(z.read(f'Audio/{prefix}_{i:03d}.ogg')))
        clips[group].append(write_clip(pkg, group + '_' + str(i), data, sr, .65))

pkg = package('springyspringo-mechanics', 'Gun reload sounds', 'SpringySpringo', 'CC0-1.0',
              'https://opengameart.org/content/gun-reload-sounds', 'assault-reload.wav',
              note='Airsoft weapon recordings; mono conversion, level adjustment and edge fades by Driftlab.')
data, sr = sf.read(DOWNLOADS / 'assault-reload.wav')
clips['nachladen'] = [write_clip(pkg, 'nachladen', data, sr, .6)]
pkg[1]['notes'] += f" Source assault-reload.wav: SHA-256 {digest((DOWNLOADS / 'assault-reload.wav').read_bytes())}."
data, sr = sf.read(DOWNLOADS / 'weapon-action.wav')
write_clip(pkg, 'waffe_wechsel', data, sr, .6)
pkg[1]['notes'] += f" Unused alternate weapon-action.wav retained: SHA-256 {digest((DOWNLOADS / 'weapon-action.wav').read_bytes())}."

pkg = package('lfa-equipment-clicks', 'Equipment clicks III', 'LFA', 'CC0-1.0',
              'https://opengameart.org/content/equipment-clicks-iii', 'equipment-clicks3.wav',
              note='The source set records a real bolt-action rifle alongside other mechanical objects. Three isolated excerpts, mono conversion, level adjustment and edge fades by Driftlab.')
data, sr = sf.read(DOWNLOADS / 'equipment-clicks3.wav')
clips['waffe_wechsel'] = []
for i, (start, end) in enumerate(((.12, .59), (10.45, 10.88), (18.84, 19.34))):
    clips['waffe_wechsel'].append(write_clip(
        pkg, f'waffe_wechsel_{i}', data[int(start * sr):int(end * sr)], sr, .62))

pkg = package('rubberduck-bangs', '25 CC0 bang / firework SFX', 'Rubberduck', 'CC0-1.0',
              'https://opengameart.org/content/25-cc0-bang-firework-sfx', 'bangs.zip',
              note='Recorded firework and cannon reports used for the bomb blast. Bass reinforcement, mono conversion, level adjustment and edge fades by Driftlab.')
z = zipfile.ZipFile(DOWNLOADS / 'bangs.zip')
clips['bombe_explosion'] = []
for i in range(1, 4):
    data, sr = sf.read(io.BytesIO(z.read(f'cannon_{i:02d}.ogg')))
    if data.ndim > 1:
        data = data.mean(axis=1)
    spectrum = np.fft.rfft(data)
    frequencies = np.fft.rfftfreq(len(data), 1 / sr)
    spectrum[frequencies > 240] = 0
    low = np.fft.irfft(spectrum, n=len(data))
    data = np.tanh((data + .38 * low) * .9)
    clips['bombe_explosion'].append(write_clip(pkg, f'bombe_explosion_{i - 1}', data, sr, .72))

pkg = package('bart-workshop', '68 Workshop Sounds', 'bart', 'CC0-1.0',
              'https://opengameart.org/content/68-workshop-sounds', 'workshop.7z',
              note='Garage tool recordings made with a Tascam DR-05. Short metal-drag and scrape excerpts, mono conversion, level adjustment and edge fades by Driftlab.')
clips['metall_knarzen'] = []
workshop = DOWNLOADS / 'workshop-extracted/workshop'
for i, (filename, start, end) in enumerate((
    ('workshop - dragging metal.wav', .7, 3.0),
    ('workshop - dragging metal.wav', 5.0, 7.4),
    ('workshop - quiet scrape.wav', .2, 2.7),
)):
    data, sr = sf.read(workshop / filename)
    clips['metall_knarzen'].append(write_clip(
        pkg, f'metall_knarzen_{i}', data[int(start * sr):int(end * sr)], sr, .58))

pkg = package('mikeask-breathing', 'Breathing tired', 'mikeask', 'CC0-1.0',
              'https://opengameart.org/content/breathing-tired', 'breathing-tired.wav',
              note='Human breathing recording; short excerpts, mono conversion, level adjustment and edge fades by Driftlab.')
data, sr = sf.read(DOWNLOADS / 'breathing-tired.wav')
for name, start, end in [('atem_ein', .02, .56), ('atem_aus', 1.12, 1.74), ('atem_keuchen', 2.40, 2.97), ('atem_schnappen', .12, .49)]:
    clips[name] = [write_clip(pkg, name, data[int(start * sr):int(end * sr)], sr, .4)]

pkg = package('legit-audio-room', 'The Shop — refrigerator room tone', 'LEGIT Audio', 'CC0-1.0',
              'https://opengameart.org/content/the-shop', 'shop-room-tone.zip',
              note='Recorded appliance drone used as hall ventilation. Mono conversion, resampling, loop crossfade and level adjustment by Driftlab.')
z = zipfile.ZipFile(DOWNLOADS / 'shop-room-tone.zip')
data, sr = sf.read(io.BytesIO(z.read('TheShopCollection_convenience_store_drinks_fridge_drone.wav')))
clips['raumton'] = [write_clip(pkg, 'raumton', data, sr, .25, True)]

pkg = package('yd-factory-music', 'Factory ambiance', 'yd', 'CC0-1.0',
              'https://opengameart.org/content/factory-ambiance', 'factory-music.ogg',
              note='Electronic composition. Menu edit, tension excerpt, short round-start edit, fades and level adjustment by Driftlab.')
data, sr = sf.read(DOWNLOADS / 'factory-music.ogg')
for name, start, end, loop in [('musik_menue', 0, 123, True), ('musik_spannung', 60, 92, True), ('musik_rundenstart', 42, 45, False)]:
    x = data[int(start * sr):int(end * sr)].copy()
    if not loop:
        n = min(sr, len(x)); x[-n:] *= np.linspace(1, 0, n)[:, None]
    clips[name] = [write_clip(pkg, name, x, sr, .35, loop)]

catalog_path = ROOT / 'Assets/_Project/Resources/audio-catalog.json'
catalog = json.loads(catalog_path.read_text())
catalog_entries = {entry['id']: entry for entry in catalog['entries']}
for sound_id, clip_key, kind, credit, gain in (
    ('WaffeWechsel', 'waffe_wechsel', 'recording', 'lfa-equipment-clicks', .92),
    ('BombeExplosion', 'bombe_explosion', 'recording', 'rubberduck-bangs', 1.0),
    ('MetallKnarzen', 'metall_knarzen', 'recording', 'bart-workshop', .82),
):
    catalog_entries[sound_id].update({
        'clips': clips[clip_key], 'kind': kind, 'credit': credit, 'reason': '', 'gain': gain,
    })
catalog['entries'] = [catalog_entries[entry['id']] for entry in catalog['entries']]
catalog_path.write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + '\n')

manifest['entries'] = list(entries.values())
manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + '\n')
(PROOF / 'clip-analysis.json').write_text(json.dumps(stats, indent=2) + '\n')
(DOWNLOADS.parent / 'audio-resource-map.json').write_text(json.dumps(clips, indent=2) + '\n')
print(f'Prepared {len(stats)} clips; no source files removed.')
