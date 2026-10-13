@ECHO OFF
for %n in (ColorPickerForm.cs ColorPickerForm.Designer.cs ColorPickerForm.resx) do mklink "%n" C:\Projects\SW\lib\%n"