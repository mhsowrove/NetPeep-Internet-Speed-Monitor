Add-Type -AssemblyName System.Drawing
$bitmap=New-Object System.Drawing.Bitmap 1024,1024
$g=[System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode='AntiAlias';$g.ScaleTransform(4,4);$g.Clear([System.Drawing.Color]::Transparent)
$path=New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc(4,4,116,116,180,90);$path.AddArc(136,4,116,116,270,90);$path.AddArc(136,136,116,116,0,90);$path.AddArc(4,136,116,116,90,90);$path.CloseFigure()
$gradient=New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0,4)),([System.Drawing.Point]::new(0,252)),([System.Drawing.ColorTranslator]::FromHtml('#167ac1')),([System.Drawing.ColorTranslator]::FromHtml('#154472'))
$g.FillPath($gradient,$path)
function PenFor($hex) { $pen=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml($hex)),13; $pen.StartCap='Round';$pen.EndCap='Round';$pen.LineJoin='Round';return $pen }
$wifi=PenFor '#e9f6ff';$up=PenFor '#ffc968';$down=PenFor '#81e0ff'
$g.DrawBezier($wifi,63,91,99,59,157,59,193,91)
$g.DrawBezier($wifi,87,116,111,96,145,96,169,116)
$dot=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#e9f6ff'));$g.FillEllipse($dot,120,131,16,16)
$g.DrawLine($up,87,211,87,156);$g.DrawLines($up,[System.Drawing.Point[]]@([System.Drawing.Point]::new(69,174),[System.Drawing.Point]::new(87,156),[System.Drawing.Point]::new(105,174)))
$g.DrawLine($down,169,157,169,212);$g.DrawLines($down,[System.Drawing.Point[]]@([System.Drawing.Point]::new(151,194),[System.Drawing.Point]::new(169,212),[System.Drawing.Point]::new(187,194)))
$bitmap.Save((Join-Path $PSScriptRoot '..\assets\icon.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$entries=@();foreach($size in @(16,32,48,64,128,256)){
  $small=New-Object System.Drawing.Bitmap $size,$size;$sg=[System.Drawing.Graphics]::FromImage($small);$sg.InterpolationMode='HighQualityBicubic';$sg.DrawImage($bitmap,0,0,$size,$size)
  $memory=New-Object System.IO.MemoryStream;$small.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png);$entries+=@{size=$size;data=$memory.ToArray()};$memory.Dispose();$sg.Dispose();$small.Dispose()
}
$file=[System.IO.File]::Create((Join-Path $PSScriptRoot '..\assets\icon.ico'));$writer=New-Object System.IO.BinaryWriter $file
$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$entries.Count);$offset=6+16*$entries.Count
foreach($entry in $entries){$dimension=if($entry.size -eq 256){0}else{$entry.size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$entry.data.Length);$writer.Write([uint32]$offset);$offset+=$entry.data.Length}
foreach($entry in $entries){$writer.Write([byte[]]$entry.data)};$writer.Close()
$tray=New-Object System.Drawing.Bitmap 40,40;$tg=[System.Drawing.Graphics]::FromImage($tray);$tg.SmoothingMode='AntiAlias';$tg.Clear([System.Drawing.Color]::Transparent)
$tp=New-Object System.Drawing.Pen ([System.Drawing.Color]::Black),3;$tp.StartCap='Round';$tp.EndCap='Round'
$tg.DrawBezier($tp,5,13,14,5,26,5,35,13);$tg.DrawBezier($tp,12,20,17,16,23,16,28,20)
$tg.DrawLine($tp,13,35,13,26);$tg.DrawLines($tp,[System.Drawing.Point[]]@([System.Drawing.Point]::new(9,30),[System.Drawing.Point]::new(13,26),[System.Drawing.Point]::new(17,30)))
$tg.DrawLine($tp,27,26,27,35);$tg.DrawLines($tp,[System.Drawing.Point[]]@([System.Drawing.Point]::new(23,31),[System.Drawing.Point]::new(27,35),[System.Drawing.Point]::new(31,31)))
$tray.Save((Join-Path $PSScriptRoot '..\assets\tray.png'),[System.Drawing.Imaging.ImageFormat]::Png);$tg.Dispose();$tray.Dispose();$tp.Dispose()
$g.Dispose();$bitmap.Dispose();$path.Dispose();$gradient.Dispose();$wifi.Dispose();$up.Dispose();$down.Dispose();$dot.Dispose()
