@echo off
echo =======================================================================
echo   Pushing All Updates and Published Output to GitHub
echo   Target: https://github.com/Abhay920920/Law_Project- (branch: main)
echo =======================================================================
set "PATH=%PATH%;C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd"
git status
git add -A
git commit -m "fix(causelist): sanitize dummy placeholder dates and normalize hearing listings with updated published release"
git push origin main
echo =======================================================================
echo   PUSH COMPLETE!
echo =======================================================================
