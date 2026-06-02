Add-Type -AssemblyName System.Drawing

$root = Join-Path $PSScriptRoot "com.abdulla.pccompanion.sdPlugin\imgs"

function C([string]$hex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
function Brush([string]$hex) { New-Object System.Drawing.SolidBrush (C $hex) }
function Pen([string]$hex, [float]$w) {
	$p = New-Object System.Drawing.Pen (C $hex), $w
	$p.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
	$p.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
	$p.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
	$p
}

function Text($g, [string]$value, [float]$x, [float]$y, [float]$w, [float]$h, [float]$size, [string]$color) {
	$font = New-Object System.Drawing.Font("Segoe UI", $size, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
	$brush = Brush $color
	$fmt = New-Object System.Drawing.StringFormat
	$fmt.Alignment = [System.Drawing.StringAlignment]::Center
	$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
	$g.DrawString($value, $font, $brush, (New-Object System.Drawing.RectangleF($x, $y, $w, $h)), $fmt)
	$fmt.Dispose(); $brush.Dispose(); $font.Dispose()
}

function RoundRect($g, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r, [string]$color) {
	$path = New-Object System.Drawing.Drawing2D.GraphicsPath
	$d = $r * 2
	$path.AddArc($x, $y, $d, $d, 180, 90)
	$path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
	$path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
	$path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
	$path.CloseFigure()
	$brush = Brush $color
	$g.FillPath($brush, $path)
	$brush.Dispose(); $path.Dispose()
}

function Draw-Symbol($g, [string]$name, [float]$s, [string]$accent) {
	$p = Pen $accent ([Math]::Max(2.0, $s * 0.055))
	switch ($name) {
		"show-popup" {
			$g.DrawRectangle($p, $s*.30, $s*.24, $s*.40, $s*.30)
			$g.DrawLine($p, $s*.42, $s*.68, $s*.50, $s*.55)
			$g.DrawLine($p, $s*.58, $s*.68, $s*.50, $s*.55)
		}
		"toggle-gopher" {
			$g.DrawEllipse($p, $s*.26, $s*.30, $s*.48, $s*.28)
			$g.DrawLine($p, $s*.36, $s*.44, $s*.48, $s*.44)
			$g.DrawLine($p, $s*.42, $s*.38, $s*.42, $s*.50)
			$g.DrawEllipse($p, $s*.58, $s*.40, $s*.04, $s*.04)
		}
		"switch-audio" {
			$g.DrawArc($p, $s*.25, $s*.22, $s*.20, $s*.36, 90, 180)
			$g.DrawArc($p, $s*.55, $s*.22, $s*.20, $s*.36, 270, 180)
			$g.DrawLine($p, $s*.50, $s*.58, $s*.50, $s*.72)
		}
		"prayer" {
			$g.DrawLine($p, $s*.28, $s*.72, $s*.72, $s*.72)
			$g.DrawLine($p, $s*.32, $s*.72, $s*.32, $s*.42)
			$g.DrawLine($p, $s*.68, $s*.72, $s*.68, $s*.42)
			$g.DrawArc($p, $s*.36, $s*.33, $s*.28, $s*.34, 180, 180)
			$g.DrawLine($p, $s*.50, $s*.28, $s*.50, $s*.18)
		}
		"toggle-hdr" {
			$g.DrawEllipse($p, $s*.31, $s*.23, $s*.38, $s*.38)
			Text $g "HDR" 0 ($s*.34) $s ($s*.18) ([Math]::Max(7, $s*.12)) $accent
		}
		"toggle-auto-hdr" {
			$g.DrawArc($p, $s*.28, $s*.24, $s*.44, $s*.44, 35, 300)
			Text $g "A" 0 ($s*.34) $s ($s*.18) ([Math]::Max(8, $s*.16)) $accent
		}
		"dial-brightness" {
			# Sun centred at (.50,.50): filled centre + 8 equal-length rays (no outer ring).
			$b = Brush $accent
			$g.FillEllipse($b, $s*.42, $s*.42, $s*.16, $s*.16)
			$b.Dispose()
			# Cardinal rays (inner r .13 -> outer r .22)
			$g.DrawLine($p, $s*.50, $s*.37, $s*.50, $s*.28)
			$g.DrawLine($p, $s*.50, $s*.63, $s*.50, $s*.72)
			$g.DrawLine($p, $s*.37, $s*.50, $s*.28, $s*.50)
			$g.DrawLine($p, $s*.63, $s*.50, $s*.72, $s*.50)
			# Diagonal rays (same radii)
			$g.DrawLine($p, $s*.408, $s*.408, $s*.344, $s*.344)
			$g.DrawLine($p, $s*.592, $s*.408, $s*.656, $s*.344)
			$g.DrawLine($p, $s*.408, $s*.592, $s*.344, $s*.656)
			$g.DrawLine($p, $s*.592, $s*.592, $s*.656, $s*.656)
		}
		"dial-sdr" {
			# Contrast symbol centred at (.50,.50): ring with the right half filled.
			$g.DrawEllipse($p, $s*.30, $s*.30, $s*.40, $s*.40)
			$b = Brush $accent
			$g.FillPie($b, $s*.30, $s*.30, $s*.40, $s*.40, -90, 180)
			$b.Dispose()
		}
		"toggle-couch-mode" {
			$g.DrawLine($p, $s*.28, $s*.44, $s*.72, $s*.44)
			$g.DrawArc($p, $s*.21, $s*.34, $s*.18, $s*.22, 90, 180)
			$g.DrawArc($p, $s*.61, $s*.34, $s*.18, $s*.22, 270, 180)
			$g.DrawLine($p, $s*.30, $s*.62, $s*.70, $s*.62)
		}
		"toggle-morning-mode" {
			$b = Brush $accent
			$g.FillEllipse($b, $s*.43, $s*.30, $s*.14, $s*.14)
			$b.Dispose()
			$g.DrawArc($p, $s*.28, $s*.28, $s*.44, $s*.44, 200, 140)
			$g.DrawLine($p, $s*.30, $s*.66, $s*.70, $s*.66)
		}
	}
	$p.Dispose()
}

function New-Image([string]$path, [int]$size, [string]$name, [string]$label, [string]$accent, [bool]$key) {
	$bmp = New-Object System.Drawing.Bitmap($size, $size)
	$g = [System.Drawing.Graphics]::FromImage($bmp)
	$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
	$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

	if ($name -like "dial-*") {
		# Transparent background, glyph only — like the Elgato standard dial icons,
		# so it blends into the circular dial area instead of showing a square.
		$g.Clear([System.Drawing.Color]::Transparent)
		Draw-Symbol $g $name $size $accent
	} elseif ($key) {
		$g.Clear((C "#0D1016"))
		RoundRect $g ($size*.08) ($size*.08) ($size*.84) ($size*.84) ($size*.12) "#161B23"
		Draw-Symbol $g $name $size $accent
	} else {
		$g.Clear((C "#0D1016"))
		RoundRect $g 0 0 $size $size ([Math]::Max(4, $size*.18)) "#161B23"
		Text $g $label 0 0 $size $size ([Math]::Max(8, $size*.30)) $accent
	}

	$dir = Split-Path $path -Parent
	if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
	$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
	$g.Dispose(); $bmp.Dispose()
}

$actions = @{
	"show-popup"          = @{ Label = "POP"; Accent = "#00D1FF" }
	"toggle-gopher"       = @{ Label = "G360"; Accent = "#5BE37D" }
	"switch-audio"        = @{ Label = "AUDIO"; Accent = "#FFD23F" }
	"prayer"              = @{ Label = "PRAY"; Accent = "#F8D878" }
	"toggle-hdr"          = @{ Label = "HDR"; Accent = "#F4F7FB" }
	"toggle-auto-hdr"     = @{ Label = "AUTO"; Accent = "#00D1FF" }
	"dial-brightness"     = @{ Label = "BRT"; Accent = "#20D6FF" }
	"dial-sdr"            = @{ Label = "SDR"; Accent = "#A8FFB8" }
	"toggle-couch-mode"   = @{ Label = "COUCH"; Accent = "#FF5C5C" }
	"toggle-morning-mode" = @{ Label = "MORN"; Accent = "#FFA94D" }
}

foreach ($name in $actions.Keys) {
	$meta = $actions[$name]
	$dir = Join-Path $root "actions\$name"
	New-Image (Join-Path $dir "icon.png") 20 $name $meta.Label $meta.Accent $false
	New-Image (Join-Path $dir "icon@2x.png") 40 $name $meta.Label $meta.Accent $false
	New-Image (Join-Path $dir "key.png") 72 $name $meta.Label $meta.Accent $true
	New-Image (Join-Path $dir "key@2x.png") 144 $name $meta.Label $meta.Accent $true
}

New-Image (Join-Path $root "plugin\category-icon.png") 28 "show-popup" "PC" "#00D1FF" $false
New-Image (Join-Path $root "plugin\category-icon@2x.png") 56 "show-popup" "PC" "#00D1FF" $false
New-Image (Join-Path $root "plugin\marketplace.png") 256 "show-popup" "PC" "#00D1FF" $true
New-Image (Join-Path $root "plugin\marketplace@2x.png") 512 "show-popup" "PC" "#00D1FF" $true

Write-Output "Icons generated under $root"
