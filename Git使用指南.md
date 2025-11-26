# Git 使用指南 - AutoDaily 项目

## 一、在 Mac 上上传到 GitHub

### 1. 初始化 Git 仓库（如果还没有）

```bash
cd /Users/yingdao/Documents/Project/日报通
git init
```

### 2. 添加所有文件到暂存区

```bash
git add .
```

### 3. 提交代码

```bash
git commit -m "Initial commit: AutoDaily 日报助手项目"
```

### 4. 在 GitHub 上创建新仓库

1. 登录 GitHub (https://github.com)
2. 点击右上角 `+` → `New repository`
3. 填写仓库名称（如：`AutoDaily`）
4. **不要**勾选 "Initialize this repository with a README"（因为本地已有代码）
5. 点击 `Create repository`

### 5. 连接远程仓库并推送

```bash
# 添加远程仓库（替换 YOUR_USERNAME 和 REPO_NAME）
git remote add origin https://github.com/YOUR_USERNAME/REPO_NAME.git

# 或者使用 SSH（如果已配置 SSH key）
# git remote add origin git@github.com:YOUR_USERNAME/REPO_NAME.git

# 推送代码到 GitHub
git branch -M main
git push -u origin main
```

## 二、在 Windows 电脑上下载和开发

### 方法一：使用 Git 命令行（推荐）

#### 1. 安装 Git for Windows

- 下载：https://git-scm.com/download/win
- 安装时选择默认选项即可

#### 2. 克隆仓库

打开 **Git Bash** 或 **PowerShell**，执行：

```bash
# 进入工作目录（如 D:\Projects）
cd D:\Projects

# 克隆仓库
git clone https://github.com/YOUR_USERNAME/REPO_NAME.git

# 进入项目目录
cd REPO_NAME
```

#### 3. 使用 Visual Studio 打开项目

```bash
# 方法1：直接双击
# 在文件管理器中找到 AutoDaily.sln，双击打开

# 方法2：命令行打开
start AutoDaily.sln
```

### 方法二：使用 GitHub Desktop（图形界面，更简单）

#### 1. 安装 GitHub Desktop

- 下载：https://desktop.github.com/
- 安装并登录 GitHub 账号

#### 2. 克隆仓库

1. 打开 GitHub Desktop
2. 点击 `File` → `Clone repository`
3. 选择你的仓库
4. 选择本地保存路径
5. 点击 `Clone`

#### 3. 打开项目

在 GitHub Desktop 中点击 `Repository` → `Open in Visual Studio`（如果已安装）

或手动打开：在文件管理器中找到 `AutoDaily.sln`，双击打开

## 三、日常开发工作流

### 在 Windows 上开发后，推送到 GitHub

```bash
# 1. 查看修改状态
git status

# 2. 添加修改的文件
git add .

# 3. 提交修改
git commit -m "描述你的修改内容"

# 4. 推送到 GitHub
git push
```

### 在 Mac 上拉取最新代码

```bash
# 进入项目目录
cd /Users/yingdao/Documents/Project/日报通

# 拉取最新代码
git pull
```

### 在 Windows 上拉取最新代码

```bash
# 进入项目目录
cd D:\Projects\REPO_NAME

# 拉取最新代码
git pull
```

## 四、常用 Git 命令速查

```bash
# 查看状态
git status

# 查看修改内容
git diff

# 查看提交历史
git log

# 创建新分支
git checkout -b feature/新功能名称

# 切换分支
git checkout main

# 合并分支
git merge feature/新功能名称

# 撤销未提交的修改
git checkout -- 文件名

# 查看远程仓库
git remote -v
```

## 五、注意事项

### 1. 不要提交的文件

以下文件已在 `.gitignore` 中，不会上传：
- `bin/`、`obj/` - 编译输出
- `.vs/` - Visual Studio 配置
- `*.exe`、`*.dll` - 编译产物
- `tasks.json` - 用户数据（每个开发者本地不同）

### 2. 跨平台开发注意事项

- **行尾符**：Git 会自动处理 Windows (CRLF) 和 Mac/Linux (LF) 的差异
- **文件路径**：代码中使用 `Path.Combine()` 确保跨平台兼容
- **编码**：所有文件使用 UTF-8 编码

### 3. 冲突解决

如果多人同时修改同一文件，可能出现冲突：

```bash
# 拉取时如果有冲突
git pull

# 手动解决冲突后
git add .
git commit -m "解决冲突"
git push
```

## 六、推荐工作流程

### 标准流程

1. **开始工作前**：`git pull` 拉取最新代码
2. **开发中**：正常编写代码
3. **完成功能后**：
   ```bash
   git add .
   git commit -m "完成功能：xxx"
   git push
   ```
4. **每天结束前**：确保代码已推送

### 分支策略（可选）

- `main` - 主分支，稳定版本
- `develop` - 开发分支
- `feature/xxx` - 功能分支

## 七、快速开始检查清单

- [ ] Mac 上：已创建 GitHub 仓库并推送代码
- [ ] Windows 上：已安装 Git 和 Visual Studio
- [ ] Windows 上：已克隆仓库到本地
- [ ] Windows 上：已成功打开 `AutoDaily.sln`
- [ ] Windows 上：已成功编译项目（无错误）

## 八、遇到问题？

### 问题1：`git push` 需要输入用户名密码

**解决**：使用 Personal Access Token
1. GitHub → Settings → Developer settings → Personal access tokens
2. 生成新 token
3. 使用 token 作为密码

### 问题2：Visual Studio 找不到项目

**解决**：确保安装了 .NET Framework 4.7.2 Developer Pack

### 问题3：编译错误

**解决**：检查 `编译说明.md` 中的常见问题部分

