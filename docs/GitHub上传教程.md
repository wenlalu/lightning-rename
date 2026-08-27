# GitHub 上传新手教程

本教程手把手教你如何把「闪电重命名」项目上传到 GitHub。

---

## 第一步：注册并登录 GitHub

1. 打开浏览器，访问 https://github.com
2. 如果你已经注册了账号，点击右上角「Sign in」登录
3. 如果还没注册，点击「Sign up」按提示完成注册（需要邮箱验证）

---

## 第二步：创建新仓库（Repository）

仓库就是存放项目代码的地方。

1. 登录后，点击右上角的 **+** 号，选择 **New repository**
   - 或者直接访问 https://github.com/new
2. 填写仓库信息：
   - **Repository name**（仓库名）：输入 `LightningRename`（或你喜欢的名字）
   - **Description**（描述）：可选，输入「轻量级 Windows 批量文件重命名工具」
   - **Public / Private**：选择 **Public**（公开，所有人都能看到）
     - 如果不想公开，选 Private（只有你和你授权的人能看到）
   - **Initialize this repository with**：**全部不要勾选**（因为我们要上传已有文件）
3. 点击底部绿色按钮 **Create repository**

创建成功后，你会看到一个空仓库页面，页面上会显示一些命令。**保持这个页面打开**，后面会用到。

---

## 第三步：安装 Git（如果还没装）

Git 是上传代码的工具。

1. 访问 https://git-scm.com/download/win
2. 下载会自动开始，运行下载的安装程序
3. 安装时一路点「Next」用默认设置即可
4. 安装完成后，**重启电脑**（或重启命令行）

验证安装：按 `Win+R`，输入 `cmd` 回车，在黑色窗口中输入：
```
git --version
```
如果显示类似 `git version 2.xx.x.windows.x`，说明安装成功。

---

## 第四步：配置 Git（只需第一次）

打开命令行（Win+R → 输入 `cmd` → 回车），输入以下两条命令（把引号里的内容换成你自己的）：

```
git config --global user.name "你的GitHub用户名"
git config --global user.email "你的GitHub注册邮箱"
```

例如：
```
git config --global user.name "zhangsan"
git config --global user.email "zhangsan@example.com"
```

---

## 第五步：上传文件

### 方法 A：命令行上传（推荐，最稳定）

1. 找到项目根目录（本地 Git 仓库）：
   `C:\Users\q\Desktop\000\重命名`
   
   这个文件夹里应该有这些文件：
   - `.gitignore`
   - `LICENSE`
   - `README.md`
   - `LightningRename.sln`
   - `LightningRename\` 文件夹（里面是源代码）

2. 在这个文件夹的地址栏中输入 `cmd` 然后按回车
   - 这样会直接在当前目录打开命令行窗口

3. 依次输入以下命令（每输完一行按回车）：

   ```
   git init
   ```
   （初始化 Git 仓库）

   ```
   git add .
   ```
   （添加所有文件，注意后面有个点 `.`）

   ```
   git commit -m "初始提交：闪电重命名 v1.0"
   ```
   （提交文件，引号里是提交说明，可以随便写）

4. 回到 GitHub 仓库页面，找到页面上显示的仓库地址，格式类似：
   `https://github.com/你的用户名/LightningRename.git`
   
   复制这个地址。

5. 在命令行输入（把地址换成你复制的）：
   ```
   git remote add origin https://github.com/你的用户名/LightningRename.git
   ```

6. 最后输入：
   ```
   git push -u origin main
   ```
   （如果提示输入用户名密码，输入 GitHub 用户名和密码；如果密码不行，需要用 Token，见下方说明）

7. 刷新 GitHub 仓库页面，你应该能看到上传的文件了！

### 方法 B：网页直接拖拽上传（最简单，但不推荐大项目）

1. 在 GitHub 仓库页面，点击 **uploading an existing file** 链接
2. 打开文件资源管理器，进入 `C:\Users\q\Desktop\000\重命名`
3. 把里面的所有文件和文件夹**全部选中**，拖拽到网页的虚线框中
4. 等待上传完成
5. 在页面底部「Commit changes」区域，输入提交说明（如「初始提交」）
6. 点击绿色按钮 **Commit changes**

> 注意：网页拖拽方式有时无法正确上传空文件夹或隐藏文件（如 .gitignore），推荐用方法 A。

---

## 常见问题

### Q: 上传时提示密码错误怎么办？

GitHub 从 2021 年起不再支持账号密码登录 Git，需要用「Personal Access Token」代替密码：

1. 登录 GitHub，点击右上角头像 → **Settings**
2. 左侧菜单拉到底，点击 **Developer settings**
3. 点击 **Personal access tokens** → **Tokens (classic)**
4. 点击 **Generate new token** → **Generate new token (classic)**
5. Note 填「Git上传」，Expiration 选「No expiration」
6. 勾选 **repo**（第一个大项，会自动勾选子项）
7. 拉到底点击 **Generate token**
8. **复制生成的 token**（只显示一次，丢了就要重新生成）
9. 上传时密码处粘贴这个 token 即可

### Q: 提示 `fatal: remote origin already exists` 怎么办？

说明之前添加过远程地址，输入以下命令删除后重新添加：
```
git remote remove origin
```
然后重新执行 `git remote add origin ...` 命令。

### Q: 提示 `error: failed to push some refs` 怎么办？

可能是 GitHub 仓库初始化时勾选了 README 等文件，导致本地和远程不一致。输入：
```
git pull origin main --allow-unrelated-histories
```
然后再 `git push -u origin main`。

### Q: 上传后想修改代码怎么办？

1. 在本地修改文件
2. 在项目目录打开 cmd，输入：
   ```
   git add .
   git commit -m "修改说明"
   git push
   ```
3. 刷新 GitHub 页面即可看到更新

---

## 上传完成后建议做的事

1. **设置仓库主题**：在仓库页面点击右上角齿轮 ⚙️（About 区域），可以添加项目描述、网站链接、主题标签（如 `csharp` `dotnet` `file-rename` `windows`）
2. **添加 Release**：点击仓库顶部的 **Releases** → **Create a new release**，可以上传编译好的 `闪电重命名.exe` 供用户直接下载
3. **开启 Issues**：用户可以通过 Issues 反馈 bug 和建议
4. **添加 Wiki**：可以写更详细的使用文档

---

## 上传包文件清单

你需要上传的就是 `GitHub上传包\LightningRename\` 文件夹里的**全部内容**：

| 文件/文件夹 | 说明 |
|---|---|
| `.gitignore` | Git 忽略规则（忽略编译产物等） |
| `LICENSE` | MIT 开源许可证 |
| `README.md` | 项目说明文档（GitHub 会自动显示在仓库首页） |
| `LightningRename.sln` | Visual Studio 解决方案文件 |
| `LightningRename\` | 项目文件夹 |
| `LightningRename\LightningRename.csproj` | 项目文件 |
| `LightningRename\Program.cs` | 程序入口 |
| `LightningRename\MainForm.cs` | 主窗体 |
| `LightningRename\Engine.cs` | 重命名核心引擎 |
| `LightningRename\Rules.cs` | 规则配置类 |
| `LightningRename\Item.cs` | 文件项数据类 |
| `LightningRename\UndoLog.cs` | 撤销日志 |
| `LightningRename\Properties\AssemblyInfo.cs` | 程序集信息 |

**不需要上传的**（已被 .gitignore 忽略）：
- `bin\` 文件夹（编译产物）
- `obj\` 文件夹（中间文件）
- `.vs\` 文件夹（Visual Studio 缓存）
- `*.exe`、`*.dll`、`*.pdb`（编译生成的二进制文件）
