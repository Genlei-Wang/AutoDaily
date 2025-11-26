#!/bin/bash
# 使用方法：./一键推送.sh https://github.com/用户名/仓库名.git

if [ -z "$1" ]; then
    echo "使用方法: ./一键推送.sh https://github.com/Genlei-Wang/AutoDaily.git"
    exit 1
fi

git remote remove origin 2>/dev/null
git remote add origin "$1"
git branch -M main
git push -u origin main

echo "✅ 完成！现在去 GitHub 仓库的 Actions 页面下载编译结果"

