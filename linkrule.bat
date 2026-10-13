@ECHO OFF
for %n in (RuleEditForm.cs RuleEditForm.Designer.cs RuleEditForm.resx) do mklink "%n" C:\Projects\SW\lib\%n"