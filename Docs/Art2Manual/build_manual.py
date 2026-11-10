from pathlib import Path
import re, json, subprocess, html, hashlib
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, KeepTogether, Flowable
from reportlab.platypus.tableofcontents import TableOfContents
from reportlab.lib.pagesizes import A4

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/pdf'; OUT.mkdir(parents=True, exist_ok=True)
TMP = ROOT / 'tmp/pdfs'; TMP.mkdir(parents=True, exist_ok=True)
FONT = 'C:/Windows/Fonts/msjh.ttc'
pdfmetrics.registerFont(TTFont('TC', FONT, subfontIndex=0))
pdfmetrics.registerFont(TTFont('TCBold', 'C:/Windows/Fonts/msjhbd.ttc', subfontIndex=0))
pdfmetrics.registerFontFamily('TC',normal='TC',bold='TCBold',italic='TC',boldItalic='TCBold')
INK=colors.HexColor('#153345'); TEAL=colors.HexColor('#127f88'); GRAY=colors.HexColor('#526475')
styles={
 'body':ParagraphStyle('body',fontName='TC',fontSize=10,leading=16,spaceAfter=9,wordWrap='CJK',textColor=INK),
 'small':ParagraphStyle('small',fontName='TC',fontSize=8,leading=12,wordWrap='CJK',textColor=GRAY),
 'cell':ParagraphStyle('cell',fontName='TC',fontSize=8.5,leading=13,wordWrap='CJK',textColor=INK),
 'h1':ParagraphStyle('h1',fontName='TCBold',fontSize=21,leading=30,spaceAfter=16,wordWrap='CJK',textColor=INK),
 'h2':ParagraphStyle('h2',fontName='TCBold',fontSize=12,leading=19,spaceBefore=10,spaceAfter=7,textColor=TEAL,wordWrap='CJK'),
 'cover':ParagraphStyle('cover',fontName='TCBold',fontSize=32,leading=46,spaceAfter=22,wordWrap='CJK',textColor=INK),
}
def P(s,style='body'): return Paragraph(html.escape(str(s)).replace('\n','<br/>'),styles[style])
def table(rows,widths=None):
    t=Table([[P(c,'cell') for c in r] for r in rows],colWidths=widths or [160,335],repeatRows=1,hAlign='LEFT')
    t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#d9eaed')),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,colors.HexColor('#f3f6f8')]),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),6),('BOTTOMPADDING',(0,0),(-1,-1),6),('LINEBELOW',(0,0),(-1,0),.6,TEAL)]))
    return t
class FlowDiagram(Flowable):
    def __init__(self): Flowable.__init__(self); self.width=495;self.height=280
    def draw(self):
        c=self.canv
        nodes=[(0,204,'Chapter / Stage','敵人波次與歌曲'),(172,204,'Hero Prefabs','隊伍與角色數值'),(344,204,'FmodBeatClock','共用音樂節拍'),(0,102,'FightLevelPlanner','開場讀取關卡'),(172,102,'FightCombatController','資源、傷害與狀態'),(344,102,'FmodRhythmJudge','輸入拍點判定'),(0,0,'FightRosterManager','站位與角色生成'),(172,0,'角色動畫 / VFX','演出與命中呈現'),(344,0,'FightScenePresenter','玩家 UI 與節拍器')]
        c.setStrokeColor(TEAL);c.setLineWidth(1.2)
        for x1,y1,x2,y2 in [(75,204,75,172),(247,204,247,172),(419,204,419,172),(150,137,172,137),(344,137,322,137),(75,102,75,70),(247,102,247,70),(322,137,419,70)]:
            c.line(x1,y1,x2,y2)
            import math
            angle=math.atan2(y2-y1,x2-x1)
            for d in [-.5,.5]: c.line(x2,y2,x2-6*math.cos(angle+d),y2-6*math.sin(angle+d))
        for x,y,a,b in nodes:
            c.setFillColor(colors.HexColor('#e8f3f4'));c.setStrokeColor(colors.HexColor('#a7c9ce'));c.roundRect(x,y,150,70,6,fill=1,stroke=1)
            t=P(a,'cell');w,h=t.wrap(134,40);t.drawOn(c,x+8,y+48-h)
            t=P(b,'small');w,h=t.wrap(134,30);t.drawOn(c,x+8,y+22-h)
def read(p): return (ROOT/p).read_text(encoding='utf-8-sig')
def blocks(text):
    result={}
    for m in re.finditer(r'^--- !u!(\d+) &(\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S):
        result[m[2]]={'type':int(m[1]),'text':m[3]}
    return result
def field(b,key,default=''):
    m=re.search(r'^  '+re.escape(key)+r':\s*([^\n]*)',b,re.M)
    return m[1].strip() if m else default
guidmap={}
for meta in list((ROOT/'Assets/FightDemo').rglob('*.meta'))+list((ROOT/'Assets/RhythmDemo').rglob('*.meta'))+list((ROOT/'Assets/InputActionMap').rglob('*.meta')):
    try:
        with meta.open(encoding='utf-8-sig') as stream: head=stream.read(300)
        m=re.search(r'^guid: (\w+)',head,re.M)
        if m: guidmap[m[1]]=str(meta.relative_to(ROOT))[:-5].replace('\\','/')
    except (OSError,UnicodeError): pass
def resolve(v):
    m=re.search(r'guid: ([a-f0-9]+)',v)
    if m:
        if m[1]=='49cb9df558623204ca97c8b6d74b7639':return '舊技能 VFX 參照；全 Assets 未找到對應 .meta，請 Inspector 檢查 Missing。'
        return guidmap.get(m[1],v)
    if v=='{fileID: 0}': return '未指定 / None'
    m=re.fullmatch(r'\{fileID: (\d+)\}',v)
    if m and 'scene' in globals() and m[1] in scene:
        obj=re.search(r'fileID: (\d+)',field(scene[m[1]]['text'],'m_GameObject'))
        if obj and obj[1] in objects:return objects[obj[1]]
    return v
scene=blocks(read('Assets/FightDemo/Scenes/Art2_FightScene.unity'))
objects={k:field(b['text'],'m_Name') for k,b in scene.items() if b['type']==1}
components={}
for k,b in scene.items():
    if b['type']!=114: continue
    script=resolve(field(b['text'],'m_Script'))
    if not script.startswith('Assets/FightDemo/') and not script.startswith('Assets/RhythmDemo/'): continue
    name=Path(script).stem
    go=re.search(r'fileID: (\d+)',field(b['text'],'m_GameObject'))
    components.setdefault(name,[]).append({'object':objects.get(go[1] if go else '', 'Prefab reference'),'text':b['text'],'fileid':k})
snapshot={'commit':subprocess.check_output(['git','rev-parse','--short','HEAD'],cwd=ROOT,text=True).strip(),'scene':'Assets/FightDemo/Scenes/Art2_FightScene.unity','components':components}
(OUT/'Art2_FightScene_snapshot.json').write_text(json.dumps(snapshot,ensure_ascii=False,indent=2),encoding='utf-8')

class Manual(SimpleDocTemplate):
    def afterFlowable(self,flow):
        if isinstance(flow,Paragraph) and flow.style.name=='h1':
            text=flow.getPlainText(); key='p'+str(self.page)
            self.canv.bookmarkPage(key); self.canv.addOutlineEntry(text,key,0)
            self.notify('TOCEntry',(0,text,self.page,key))
def footer(c,d):
    c.saveState(); c.setStrokeColor(colors.HexColor('#d0dce3')); c.line(50,45,545,45)
    c.setFont('TC',8);c.setFillColor(GRAY)
    c.drawString(50,31,'ART2 操作手冊  /  2026-11-10  /  '+snapshot['commit'])
    c.drawRightString(545,31,str(d.page));c.restoreState()
story=[]
story += [Spacer(1,76),P('THE TOASTING HERO','h2'),P('Art2_FightScene\n場景與戰鬥製作\n操作手冊','cover'),P('不依賴 Codex 的 Unity Inspector 工作指南','h2'),Spacer(1,30),P('場景、角色、關卡、動畫、特效、UI、音樂與問題排查。\n依目前專案檔案核對，保留 Inspector 英文欄位名稱。'),Spacer(1,28),table([['版本基準','記錄'],['日期','2026-11-10（台北）'],['Git 基準',snapshot['commit']+' / FightPartyControl'],['專案','D:/Project2/RhythmHunter'],['Unity','6000.3.21f1'],['使用範圍','Art2_FightScene；不把純 2D Main 場景設定混入。']]),Spacer(1,22),P('本手冊是檔案與程式核對版，不宣稱已重新實測每個組合。欄位值取自生成當下的已儲存檔案；尚未存檔的 Unity 編輯內容不在快照內。','small'),PageBreak()]
story.append(P('目錄','h1'))
toc=TableOfContents();toc.levelStyles=[ParagraphStyle('toc',fontName='TC',fontSize=10,leading=15,spaceBefore=1,textColor=INK,wordWrap='CJK')]
toc.tableStyle=TableStyle([('TOPPADDING',(0,0),(-1,-1),2),('BOTTOMPADDING',(0,0),(-1,-1),2)])
story += [toc,PageBreak()]
pages=json.loads((Path(__file__).parent/'manual_pages.json').read_text(encoding='utf-8'))
for page in pages:
    story.append(P(page['title'],'h1'))
    if page.get('lead'): story.append(P(page['lead']))
    if page.get('diagram'): story.extend([FlowDiagram(),Spacer(1,14)])
    for section in page['sections']:
        if section.get('h'): story.append(P(section['h'],'h2'))
        for text in section.get('p',[]): story.append(P(text))
        if section.get('rows'):story += [table(section['rows']),Spacer(1,10)]
    if page.get('sources'):story.append(P('核對來源：'+ '；'.join(page['sources']),'small'))
    story.append(PageBreak())

# Concise, mechanically extracted current values, separate from documented defaults.
groups=[
('附錄 A1｜戰鬥與波次現值','FightCombatController',['waveTransitionWaitBeats','waveArmorRecoveryEffect','showEnemyDamageNumbers','enableCharacterPassives','showStatusIcons','startWithFullTeamSkill','teamSkillGaugeGain','teamSkillBurstBeats','endBattleOnZeroHp','autoPlayBattleEntrance','musicStartDelaySeconds','resolutionSafetyMs','enableSuccessfulInputSound','successfulInputSoundEventPath','successfulInputSoundVolume']),
('附錄 A2｜UI 現值','FightScenePresenter',['timelineScale','showTimelineBar','timelineWidth','timelineBottomOffset','previewBeats','stoneArtSize','timelinePointArtSize','stoneBounceEnabled','stoneBounceAmount','stoneBounceBeats','stoneSuccessEffectPrefab','stoneSuccessEffectLifetime','beatPointHitEffectPrefab','showDeveloperUi','showDefenseHud','showFormationLockStatus','showVisibilityButtons','showEnemyHealthUi','showEnemyStatusUi','showTeamSkillGauge']),
('附錄 A3｜角色視覺現值','Art2ActorPresentation',['actorMaterial','informationCamera','alignSlotInformation','healthBarHeadGap','castSpriteShadows','alignFeetToGround','actorGroundHeight','showContactShadows','contactShadowMaterial','shadowSize','shadowGroundOffset','contactShadowFollowsLight','contactShadowProjectionHeight','contactShadowMaxOffset','groundHeight']),
('附錄 A4｜Camera 與節拍現值','Art2CameraController',['perspective','pitch','fieldOfView','framingHalfHeight','focus','panSpeed','zoomSpeed','zoomLimits','enableKeyboardAndWheel']),
('附錄 A5｜入場與受傷提示現值','FightWaveEntrance',['slideEnabled','durationBeats','hopOnArrival','edgeMargin','showInvulnerabilityLabel','invulnerabilityLabel','invulnerabilityColor','invulnerabilityTextSize','invulnerabilityHeadOffset','invulnerabilityHitLabelSeconds']),
]
for title,name,keys in groups:
    story.append(P(title,'h1')); story.append(P('以下是場景已儲存值，不是程式預設值。0 / 1 在布林欄位代表關閉 / 開啟。物件參照以資源路徑呈現；本頁沒有列出的欄位仍可能存在。'))
    items=components.get(name,[])
    if items:
        b=items[0]; story.append(P(b['object']+' > '+name,'h2'))
        story.append(table([['序列化欄位名稱','已儲存值']]+[[key,resolve(field(b['text'],key,'場景未明列，查元件預設'))] for key in keys],[192,303]))
    if name=='Art2CameraController':
        for extra,ks in [('FmodRhythmJudge',['perfectWindowMs','judgementOffsetMs','duplicatePerfectBecomesMiss']),('FmodBeatClock',['musicEventPath','musicVolume','musicPitch','enableBeatTapping'])]:
            b=components[extra][0];story += [P(extra,'h2'),table([['欄位','已儲存值']]+[[k,field(b['text'],k)] for k in ks],[192,303])]
    if name=='FightWaveEntrance':
        b=components['FightDamageEdgeFeedback'][0];story += [P('FightDamageEdgeFeedback','h2'),table([['欄位','已儲存值']]+[[k,field(b['text'],k)] for k in ['showDamageFlash','showLowHealthPulse','hitFlashSeconds','edgeWidth','lowHealthThreshold','lowHealthPulseBeats']],[192,303])]
    story.append(PageBreak())

story.append(P('附錄 A6｜隊伍與關卡現況','h1'))
story.append(P('Planner 目前選 Chapter01 / Stage Index 0。以下直接解析已儲存的隊伍與 Stage 資料；空格表示該站位沒有敵人，不會自動填補。'))
roster=components['FightRosterManager'][0]['text']
hero_block=re.search(r'^  heroPrefabs:\n((?:  - .*\n)+)',roster,re.M)
if hero_block:
    refs=re.findall(r'^  - (.+)',hero_block[1],re.M)
    story.append(table([['玩家位置','角色 Prefab']]+[[label,resolve(ref)] for label,ref in zip(['前衛','中衛','後衛'],refs)]))
for stage_name in ['Stage01','Stage02']:
    txt=read('Assets/FightDemo/Levels/'+stage_name+'.asset')
    story += [P(stage_name+'.asset','h2'),P('Stage Id = '+field(txt,'stageId')+'；Display Name = '+field(txt,'displayName')+'；Music = '+field(txt,'musicEventPath'),'small')]
    waves=re.findall(r'  - name: ([^\n]+)\n    front: ([^\n]+)\n    middle: ([^\n]+)\n    back: ([^\n]+)',txt)
    rows=[['Wave / 名稱','前衛 / 中衛 / 後衛']]
    for i,(name,f,m,b) in enumerate(waves):
        def short(v):
            value=resolve(v)
            return Path(value).stem if value.startswith('Assets/') else '空位'
        rows.append([str(i+1)+' / '+name,' / '.join(short(v) for v in [f,m,b])])
    story.append(table(rows))
story.append(P('整理提醒：Stage02 目前的 Stage Id 與 Display Name 仍是 Stage01 / Stage 1，而且兩波顯示名稱都叫 Wave 1。檔名不同不會自動改這些欄位；正式擴充前請手動命名，避免結算與未來存檔識別混淆。本次沒有改動這些資料。'))
story.append(PageBreak())

# Prefab combat values: root component only; nested animation overrides separately.
story.append(P('附錄 B｜角色 Prefab 數值快照','h1'))
story.append(P('資料直接來自 ArtBattle 根 Prefab 的 FightCharacterDefinition。實際隊伍由 Roster 決定；前衛護甲加成仍受 Enable Character Passives 控制。這是現況記錄，不是要求回復最初企劃。'))
prefabs=[]
for path in sorted((ROOT/'Assets/FightDemo/Prefabs/ArtBattle').glob('*.prefab')):
    for b in blocks(path.read_text(encoding='utf-8-sig')).values():
        if 'FightCharacterDefinition\n' not in b['text']:continue
        prefabs.append((path,b['text']))
for path,b in prefabs:
    heading=P(path.stem,'h2')
    rows=[['欄位','數值'],['HP / DEF / MP需求',f"{field(b,'maxHp')} / {field(b,'defense')} / {field(b,'teamSkillManaCost')}"],['普通能力 / 基礎值',field(b,'normalAbilityBehavior')+' / '+field(b,'normalAbilityPower')],['敵人每次命中 Attack Power',field(b,'attackPower')+'（敵人排程使用；勇者普通能力看上一列）'],['技能類型 / 基礎值',field(b,'skillBehavior')+' / '+field(b,'skillPower')],['攻擊間隔 / 前搖 / 圖片事件',field(b,'attackIntervalBeats')+' / '+field(b,'attackWindupBeats')+' / '+field(b,'enemyAttackUsesEffectFrames')],['普通特效',resolve(field(b,'normalAbilityEffectPrefab'))],['技能特效',resolve(field(b,'skillEffectPrefab'))]]
    story.append(KeepTogether([heading,table(rows,[180,315]),Spacer(1,8)]))
story.append(P('AbilityBehavior 對照：0 DamageFront；1 Guard；2 HealParty；3 DamageAll；4 GuardAndDamageFront；5 Unavailable；6 AimedShot；7 BreakingSlash；8 EnergySurge（舊）；9 FormationBreaker（舊）；10 RestoreArmor；11 SlimeFragments；12 SmallBombs。','small'))
story.append(P('資源核對發現：Killer / Giant 的 skillEffectPrefab 仍參照舊 GUID 49cb9df558623204ca97c8b6d74b7639，Assets 內未找到對應 .meta。這是技能欄位，不是目前正常攻擊的 Normal Ability Effect Prefab；本手冊只記錄，沒有擅自清除或替換。','small'));story.append(PageBreak())

story.append(P('附錄 C｜場景元件與維護位置','h1'))
story.append(P('以下只列 Art2 場景直接儲存的自訂 MonoBehaviour，Prefab 內元件與 Play 生成的 UI 需搭配正文查閱。不要因這張表沒列出就判定元件不存在。'))
rows=[['元件 / 物件','腳本位置']]
for name,items in sorted(components.items()):
    paths=list((ROOT/'Assets/FightDemo').rglob(name+'.cs'))+list((ROOT/'Assets/RhythmDemo').rglob(name+'.cs'))
    if paths:rows.append([name+'\n'+', '.join(sorted(set(i['object'] for i in items))),str(paths[0].relative_to(ROOT)).replace('\\','/')])
story.append(table(rows,[220,275]));story.append(PageBreak())
story.append(P('附錄 D｜快照、核對範圍與更新方式','h1'))
story.append(P('本版核對 Art2 場景 YAML、CaveEnvironment_2_5D Prefab、ArtBattle 角色資料、當前關卡、Input Actions，以及 FightDemo / RhythmDemo 相關 runtime 與 editor 程式。沒有為製作文件執行會重寫場景的 Setup 選單，也沒有重新設定主光、相機、動畫或戰鬥參數。'))
story.append(P('本版附帶 Art2_FightScene_snapshot.json 保存場景直接元件資料；manual_pages.json 是可編輯的章節內容，build_manual.py 負責重建 PDF。快照不是可直接還原場景的備份。完整還原仍使用 Unity 資源與 Git。'))
story.append(P('以日期與 Git 提交識別版本。日後改動 Prefab 時，同步記錄：資源路徑、欄位、舊值、新值、預期畫面、測試結果。不要只寫「修正一些數值」。每次大改後，優先更新快速索引、現值附錄、敵人動畫事件表及待辦清單。'))
story.append(P('已完成：來源欄位與路徑核對、PDF 中文字型嵌入、文字與頁面結構檢查、渲染排版檢查。限制：未重新遊玩所有 Stage 與所有 Prefab 組合；部分舊驗證工具包含舊企劃假設，不能把單一舊測試失敗直接當成目前功能壞掉。'))
story.append(P('專案根目錄：D:/Project2/RhythmHunter\n主場景：Assets/FightDemo/Scenes/Art2_FightScene.unity\n文件來源：Docs/Art2Manual/\n交付：output/pdf/Art2_FightScene_Manual_zh-TW.pdf','small'))
doc=Manual(str(OUT/'Art2_FightScene_Manual_zh-TW.pdf'),pagesize=A4,rightMargin=50,leftMargin=50,topMargin=49,bottomMargin=62,title='Art2_FightScene 場景與戰鬥製作操作手冊',author='The Toasting Hero - Project Handbook')
doc.multiBuild(story,onFirstPage=footer,onLaterPages=footer)
print('Created',OUT/'Art2_FightScene_Manual_zh-TW.pdf')
