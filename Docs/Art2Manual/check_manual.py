from pathlib import Path
import json
from pypdf import PdfReader
from PIL import Image,ImageOps,ImageDraw
from reportlab.pdfbase.ttfonts import TTFont
root=Path(__file__).resolve().parents[2]; tmp=root/'tmp/pdfs'
r=PdfReader(root/'output/pdf/Art2_FightScene_Manual_zh-TW.pdf')
texts=[p.extract_text() or '' for p in r.pages]
full='\n'.join(texts)
assert '場景與戰鬥製作' in full
assert 'Killer' in full and '0.051' in full
assert all(len(t)>90 for t in texts)
assert len(r.outline)>35
assert sum(len(p.get('/Annots',[])) for p in r.pages)>35
font=TTFont('check','C:/Windows/Fonts/msjh.ttc',subfontIndex=0)
missing=sorted(set(c for c in full if not c.isspace() and ord(c) not in font.face.charToGlyph))
assert not missing,repr(missing)
report={'pages':len(r.pages),'searchable_traditional_chinese':True,'bookmarks':len(r.outline),'annotations':sum(len(p.get('/Annots',[])) for p in r.pages),'missing_glyphs':missing,'page_character_counts':[len(t) for t in texts]}
(tmp/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
(tmp/'extracted_text.txt').write_text(full,encoding='utf-8')
for start in range(0,len(r.pages),12):
    sheet=Image.new('RGB',(1200,1740),'#d8e0e5');d=ImageDraw.Draw(sheet)
    for offset in range(12):
        i=start+offset
        if i>=len(r.pages):break
        file=tmp/f'final-{i+1:02}.png'
        im=Image.open(file).convert('RGB');im.thumbnail((286,410))
        x=(offset%4)*300+7;y=(offset//4)*580+30
        sheet.paste(im,(x,y));d.text((x,y-18),f'PAGE {i+1}',fill='black')
    sheet.save(tmp/f'contact-{start//12+1}.jpg',quality=90)
print(json.dumps(report))
