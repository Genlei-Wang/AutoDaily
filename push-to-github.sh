#!/bin/bash

# AutoDaily 推送到 GitHub 脚本
# 使用方法: ./push-to-github.sh

echo "========================================"
echo "AutoDaily 推送到 GitHub"
echo "========================================"
echo ""

# 检查是否在项目根目录
if [ ! -f "AutoDaily.sln" ]; then
    echo "❌ 错误：请在项目根目录运行此脚本"
    exit 1
fi

# 检查 Git 状态
if [ ! -d ".git" ]; then
    echo "❌ 错误：Git 仓库未初始化"
    exit 1
fi

echo "📋 当前 Git 状态："
git status --short
echo ""

# 检查是否有未提交的更改
if [ -n "$(git status --porcelain)" ]; then
    echo "⚠️  检测到未提交的更改"
    read -p "是否提交这些更改？(y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        read -p "请输入提交信息: " commit_msg
        if [ -z "$commit_msg" ]; then
            commit_msg="Update"
        fi
        git add .
        git commit -m "$commit_msg"
    fi
fi

echo ""
echo "📤 准备推送到 GitHub"
echo ""

# 检查是否已设置远程仓库
if git remote get-url origin > /dev/null 2>&1; then
    echo "✅ 远程仓库已设置:"
    git remote -v
    echo ""
    read -p "是否推送到 GitHub？(y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        git push -u origin main || git push -u origin master
        echo ""
        echo "✅ 推送完成！"
        echo ""
        echo "📦 下一步："
        echo "1. 访问你的 GitHub 仓库"
        echo "2. 进入 'Actions' 页面"
        echo "3. 等待编译完成（约 2-3 分钟）"
        echo "4. 下载编译好的 ZIP 文件"
    fi
else
    echo "⚠️  远程仓库未设置"
    echo ""
    echo "请先执行以下命令添加远程仓库："
    echo ""
    echo "  git remote add origin https://github.com/YOUR_USERNAME/REPO_NAME.git"
    echo "  git branch -M main"
    echo "  git push -u origin main"
    echo ""
    echo "或者运行此脚本后，按照提示操作"
    echo ""
    read -p "是否现在添加远程仓库？(y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        read -p "请输入 GitHub 仓库 URL (如: https://github.com/username/repo.git): " repo_url
        if [ -n "$repo_url" ]; then
            git remote add origin "$repo_url"
            git branch -M main
            echo ""
            read -p "是否立即推送？(y/n) " -n 1 -r
            echo
            if [[ $REPLY =~ ^[Yy]$ ]]; then
                git push -u origin main
                echo ""
                echo "✅ 推送完成！"
            fi
        fi
    fi
fi

echo ""
echo "========================================"

