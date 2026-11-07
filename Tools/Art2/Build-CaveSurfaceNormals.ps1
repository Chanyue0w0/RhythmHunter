# Authored broad surface directions, NOT a conversion of painted brightness to height.
# Coordinates refer to the unmodified 1280 x 720 source canvases (top-left origin).
# Only technical normal-map PNGs are generated. Original artwork is never written.
param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../../Assets/FightDemo/Art2Prototype'))
Add-Type -AssemblyName System.Drawing
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $outputPath)) { throw "Missing output directory: $outputPath" }

function Paint-Face($graphics, [double[]]$normal, [int[]]$points) {
    $length = [Math]::Sqrt($normal[0]*$normal[0]+$normal[1]*$normal[1]+$normal[2]*$normal[2])
    $color = [Drawing.Color]::FromArgb(255,
        [int][Math]::Round(($normal[0]/$length*.5+.5)*255),
        [int][Math]::Round(($normal[1]/$length*.5+.5)*255),
        [int][Math]::Round(($normal[2]/$length*.5+.5)*255))
    $vertices = [Drawing.Point[]]::new($points.Length/2)
    for ($i=0; $i -lt $vertices.Length; $i++) { $vertices[$i] = [Drawing.Point]::new($points[$i*2],$points[$i*2+1]) }
    $brush = [Drawing.SolidBrush]::new($color)
    try { $graphics.FillPolygon($brush, $vertices) } finally { $brush.Dispose() }
}

foreach ($kind in @('Rocks','Wood')) {
    # Author on the artwork's four-source-pixel grid to avoid sub-pixel surface seams.
    $bitmap = [Drawing.Bitmap]::new(320,180,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::None
        $graphics.Clear([Drawing.Color]::FromArgb(255,128,128,255))
        $graphics.ScaleTransform(.25,.25)
        if ($kind -eq 'Rocks') {
            # Large left rock: front-left, right flank, upper plane, smaller adjoining rock.
            Paint-Face $graphics @(-.52,.10,.85) @(104,432, 112,390, 147,365, 190,393, 222,437)
            Paint-Face $graphics @(.55,.12,.83) @(200,346, 241,384, 271,431, 221,438, 183,387)
            Paint-Face $graphics @(-.15,.67,.73) @(147,366, 165,344, 200,344, 230,379, 240,407, 201,397, 177,379)
            Paint-Face $graphics @(.45,.1,.89) @(256,386, 296,399, 328,430, 279,434)
            Paint-Face $graphics @(-.12,.64,.76) @(242,382, 258,383, 296,400, 282,412, 259,405)
            # Right group: upper face and two sides, plus the small left stone.
            Paint-Face $graphics @(-.54,.08,.84) @(1117,384, 1144,365, 1153,393, 1175,420, 1136,422)
            Paint-Face $graphics @(.52,.08,.85) @(1164,354, 1193,377, 1207,416, 1175,424, 1150,388)
            Paint-Face $graphics @(-.12,.68,.72) @(1126,374, 1147,351, 1165,351, 1185,370, 1174,388, 1149,392)
            Paint-Face $graphics @(-.35,.53,.77) @(1091,392, 1110,383, 1130,407, 1107,415)
        } else {
            # Left fence upright broad faces and narrow turned sides.
            foreach ($post in @(@(389,405,277,351),@(416,434,278,351),@(445,460,278,351),@(467,486,283,351))) {
                $l=$post[0];$r=$post[1];$t=$post[2];$b=$post[3]
                Paint-Face $graphics @(-.12,.07,.99) @($l,$t,$r,$t,$r,$b,$l,$b)
                Paint-Face $graphics @(.58,.05,.81) @(($r-5),$t,$r,$t,$r,$b,($r-5),$b)
                Paint-Face $graphics @(0,.72,.69) @($l,$t,($r-4),($t-2),$r,($t+5),$l,($t+6))
            }
            Paint-Face $graphics @(.02,.08,.99) @(369,315,491,312,501,330,373,327)
            Paint-Face $graphics @(.02,.70,.71) @(369,313,491,310,498,317,371,320)
            # Right barricade posts, slanted outer braces and crossing beams.
            foreach ($post in @(@(901,922,219,354),@(927,946,211,355),@(966,987,226,354))) {
                $l=$post[0];$r=$post[1];$t=$post[2];$b=$post[3]
                Paint-Face $graphics @(-.16,.09,.98) @($l,$t,$r,$t,$r,$b,$l,$b)
                Paint-Face $graphics @(.61,.04,.79) @(($r-6),$t,$r,$t,$r,$b,($r-6),$b)
                Paint-Face $graphics @(0,.74,.67) @($l,$t,$r,($t-4),$r,($t+6),$l,($t+7))
            }
            Paint-Face $graphics @(-.52,.24,.82) @(866,224,878,219,918,289,905,303)
            Paint-Face $graphics @(.50,.20,.84) @(952,283,967,274,1006,348,991,352)
            Paint-Face $graphics @(.10,.08,.99) @(862,302,1006,276,1012,291,859,321)
            Paint-Face $graphics @(.10,.68,.73) @(862,300,1006,274,1010,280,862,307)
            Paint-Face $graphics @(-.1,.12,.99) @(860,281,996,304,1004,321,858,296)
            Paint-Face $graphics @(-.12,.68,.72) @(860,279,996,302,999,308,860,285)
        }
        $fullCanvas = [Drawing.Bitmap]::new(1280,720,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $upscale = [Drawing.Graphics]::FromImage($fullCanvas)
        try {
            $upscale.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $upscale.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
            $upscale.DrawImage($bitmap,[Drawing.Rectangle]::new(0,0,1280,720),0,0,320,180,[Drawing.GraphicsUnit]::Pixel)
            $fullCanvas.Save((Join-Path $outputPath ("Cave"+$kind+"Normal.png")),[Drawing.Imaging.ImageFormat]::Png)
            # Exact pixel registration of the user's 2x enlarged, independently cropped art.
            $splits = if ($kind -eq 'Rocks') {
                @(@('RockLeft',100,344,232,96), @('RockRight',1080,352,148,72))
            } else {
                @(@('WoodLeft',368,276,144,68), @('WoodRight',852,208,160,144))
            }
            foreach ($split in $splits) {
                $width=[int]$split[3]*2; $height=[int]$split[4]*2
                $part=[Drawing.Bitmap]::new($width,$height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
                $partGraphics=[Drawing.Graphics]::FromImage($part)
                try {
                    $partGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                    $partGraphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
                    $partGraphics.DrawImage($fullCanvas,[Drawing.Rectangle]::new(0,0,$width,$height),
                        [int]$split[1],[int]$split[2],[int]$split[3],[int]$split[4],[Drawing.GraphicsUnit]::Pixel)
                    $part.Save((Join-Path $outputPath ('Cave'+$split[0]+'Normal.png')),[Drawing.Imaging.ImageFormat]::Png)
                } finally { $partGraphics.Dispose(); $part.Dispose() }
            }
        } finally { $upscale.Dispose(); $fullCanvas.Dispose() }
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
Write-Output 'Generated two full-canvas and four registered split normal maps; source artwork untouched.'
