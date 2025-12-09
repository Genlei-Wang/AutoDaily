#!/bin/bash
# GitHub 推送脚本 - 支持代理和 SSH 两种方式

REPO_URL="https://github.com/Genlei-Wang/AutoDaily.git"
SSH_REPO_URL="git@github.com:Genlei-Wang/AutoDaily.git"

echo "🚀 开始推送项目到 GitHub..."

# 检查是否有未提交的更改
if [ -n "$(git status --porcelain)" ]; then
    echo "⚠️  检测到未提交的更改，请先提交后再推送"
    exit 1
fi

# 方法1: 尝试使用 HTTPS（带代理检测）
try_https_push() {
    echo "📡 尝试使用 HTTPS 方式推送..."
    
    # 检查是否配置了代理
    HTTP_PROXY=$(git config --global --get http.proxy)
    HTTPS_PROXY=$(git config --global --get https.proxy)
    
    if [ -z "$HTTP_PROXY" ] && [ -z "$HTTPS_PROXY" ]; then
        echo "💡 提示: 如果网络连接失败，可以配置代理："
        echo "   git config --global http.proxy http://127.0.0.1:7890"
        echo "   git config --global https.proxy http://127.0.0.1:7890"
        echo ""
    fi
    
    git push origin main 2>&1
    return $?
}

# 方法2: 尝试使用 SSH
try_ssh_push() {
    echo "🔐 尝试使用 SSH 方式推送..."
    
    # 检查 SSH key 是否存在
    if [ ! -f ~/.ssh/id_rsa ] && [ ! -f ~/.ssh/id_ed25519 ]; then
        echo "❌ 未找到 SSH key，请先配置："
        echo "   1. 生成 SSH key: ssh-keygen -t ed25519 -C \"your_email@example.com\""
        echo "   2. 添加到 SSH agent: ssh-add ~/.ssh/id_ed25519"
        echo "   3. 将公钥添加到 GitHub: cat ~/.ssh/id_ed25519.pub"
        return 1
    fi
    
    # 切换远程 URL 到 SSH
    git remote set-url origin "$SSH_REPO_URL"
    echo "✅ 已切换远程 URL 到 SSH 方式"
    
    # 测试 SSH 连接
    if ssh -T git@github.com 2>&1 | grep -q "successfully authenticated"; then
        echo "✅ SSH 连接成功"
        git push origin main 2>&1
        return $?
    else
        echo "❌ SSH 认证失败，请检查 SSH key 配置"
        return 1
    fi
}

# 主流程
echo "📍 当前分支: $(git branch --show-current)"
echo "📍 远程仓库: $(git config --get remote.origin.url)"
echo ""

# 先尝试 HTTPS
if try_https_push; then
    echo "✅ 推送成功！"
    exit 0
fi

echo ""
echo "⚠️  HTTPS 方式失败，尝试 SSH 方式..."
echo ""

# 如果 HTTPS 失败，尝试 SSH
if try_ssh_push; then
    echo "✅ 推送成功！"
    exit 0
fi

echo ""
echo "❌ 所有推送方式都失败了"
echo ""
echo "💡 解决方案："
echo "   1. 配置 HTTP 代理（如果有代理软件）："
echo "      git config --global http.proxy http://127.0.0.1:7890"
echo "      git config --global https.proxy http://127.0.0.1:7890"
echo ""
echo "   2. 配置 SSH key（推荐）："
echo "      ssh-keygen -t ed25519 -C \"your_email@example.com\""
echo "      ssh-add ~/.ssh/id_ed25519"
echo "      # 然后将 ~/.ssh/id_ed25519.pub 的内容添加到 GitHub"
echo ""
echo "   3. 检查网络连接或稍后重试"
exit 1

