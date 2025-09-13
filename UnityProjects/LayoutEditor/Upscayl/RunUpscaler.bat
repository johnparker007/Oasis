@echo off
REM Usage:
REM upscale.bat inputImagePath outputImagePath scale modelDir modelName

cd "C:\Program Files\Upscayl\resources\bin"

upscayl-realesrgan.exe -i "%1" -o "%2" -s %3 -m "%4" -n %5
