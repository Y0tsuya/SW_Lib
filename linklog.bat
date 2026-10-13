@ECHO OFF
for %n in (log00_blank.png log01_info.png log02_alert.png log03_warn.png log04_error.png log05_extend.png) do mklink "%n" C:\Projects\SW\lib\%n"
for %n in (log06_info2.png log07_alert2.png log08_warn2.png log09_error2.png log10_extend2.png) do mklink "%n" C:\Projects\SW\lib\%n"
for %n in (LogForm.cs LogForm.Designer.cs LogForm.resx) do mklink "%n" C:\Projects\SW\lib\%n"