
/* HexHub Mock - vanilla JS */
const $ = (sel, root=document)=>root.querySelector(sel);
const $$ = (sel, root=document)=>Array.from(root.querySelectorAll(sel));

const state = {
  theme: "dark",
  env: "生产",
  viewMode: "tree", // tree | sessions
  isLeftCollapsed: false,
  isRightPaneVisible: true,
  isDrawerVisible: false,
  splitMode: "single", // single | split
  paneFocus: "left", // left | right
  sidebarWidth: 310,
  rightWidth: 360,

  // assets
  selectedAssetIds: new Set(),
  expandedFolderIds: new Set(),
  assetSearch: "",

  // tabs/panes
  panes: {
    left: { tabs: [], activeTabId: null },
    right: { tabs: [], activeTabId: null }
  },

  // mock data
  assets: null,

  // snippets/history
  vaults: [
    { id:"v1", name:"Novel" },
    { id:"v2", name:"hex" },
    { id:"v3", name:"Base" },
  ],
  activeVaultId: "v2",
  snippets: [
    { id:"s1", vaultId:"v2", title:"apt update && upgrade", content:"sudo apt update && sudo apt -y upgrade" },
    { id:"s2", vaultId:"v2", title:"tail logs", content:"tail -n 200 -f /var/log/syslog" },
    { id:"s3", vaultId:"v1", title:"git status", content:"git status -sb" },
  ],
  history: []
};

const mockAssets = () => ({
  folders: [
    {
      id:"f1", name:"interserver", children:[
        { id:"c1", type:"conn", name:"Sharon", host:"157.254.53.77", port:57722, user:"root", env:"生产" },
        { id:"c2", type:"conn", name:"sg pg", host:"66.45.226.118", port:33067, user:"root", env:"生产" },
      ]
    },
    {
      id:"f2", name:"192.168.10.52", children:[
        { id:"c3", type:"conn", name:"bqge sq 1024", host:"192.168.10.52", port:22, user:"root", env:"测试" }
      ]
    }
  ]
});

const commands = [
  { id:"toggle-sftp", title:"Toggle SFTP Pane", hint:"Alt+S", run:()=>toggleRightPane() },
  { id:"toggle-drawer", title:"Toggle Drawer", hint:"Alt+D", run:()=>toggleDrawer() },
  { id:"new-conn", title:"New Connection", hint:"N", run:()=>openConnModal({mode:"new"}) },
  { id:"new-folder", title:"New Folder", hint:"Shift+N", run:()=>createFolder() },
  { id:"open-settings", title:"Open Settings (mock)", hint:"Ctrl+,", run:()=>toast("设置","这里是占位设置面板", "good") },
  { id:"focus-search", title:"Focus Global Search", hint:"/", run:()=>$("#globalSearch").focus() },
];

function nowTime(){
  const d=new Date();
  return d.toLocaleString();
}
function pushHistory(text){
  state.history.unshift({ id: crypto.randomUUID(), time: nowTime(), text });
  renderHistory();
}
function toast(title, message, kind="good"){
  const host = $("#toastHost");
  const el = document.createElement("div");
  el.className = `Toast ${kind}`;
  el.innerHTML = `<div class="t">${escapeHtml(title)}</div><div class="m">${escapeHtml(message)}</div>`;
  host.appendChild(el);
  setTimeout(()=>{ el.style.opacity="0"; el.style.transform="translateY(6px)"; }, 2400);
  setTimeout(()=>el.remove(), 2900);
}
function escapeHtml(s){
  return (s??"").toString().replace(/[&<>"']/g, m=>({ "&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;" }[m]));
}

/* ===== Layout resizing ===== */
function setupSplitters(){
  const splitLeft = $("#splitLeft");
  const splitRight = $("#splitRight");
  let dragging = null;

  function onMove(e){
    if(!dragging) return;
    const x = e.clientX;
    const railW = 54;
    if(dragging === "left"){
      const w = Math.min(520, Math.max(240, x - railW));
      state.sidebarWidth = w;
      $("#assetSidebar").style.width = w + "px";
    }
    if(dragging === "right"){
      const appW = document.body.clientWidth;
      const w = Math.min(620, Math.max(280, appW - x));
      state.rightWidth = w;
      $("#rightPane").style.width = w + "px";
      $("#drawer").style.width = Math.min(520, Math.max(360, w+60)) + "px";
    }
  }
  function onUp(){
    dragging = null;
    document.body.style.cursor = "";
    document.body.style.userSelect = "";
    window.removeEventListener("mousemove", onMove);
    window.removeEventListener("mouseup", onUp);
  }
  function startDrag(which){
    dragging = which;
    document.body.style.cursor = "col-resize";
    document.body.style.userSelect = "none";
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  }

  splitLeft.addEventListener("mousedown", ()=>startDrag("left"));
  splitRight.addEventListener("mousedown", ()=>startDrag("right"));
}

/* ===== Topbar ===== */
function setupTopbar(){
  $("#envPill").textContent = state.env;

  $("#btnToggleSftp").addEventListener("click", toggleRightPane);
  $("#btnToggleDrawer").addEventListener("click", toggleDrawer);
  $("#btnTheme").addEventListener("click", ()=>{
    toast("主题","这里是主题切换占位（已预留）", "good");
  });
  $("#btnSettings").addEventListener("click", ()=>toast("设置","占位设置入口", "good"));
  $("#btnHelp").addEventListener("click", ()=>toast("快捷键","Ctrl+K 打开命令面板；Alt+S SFTP；Alt+D Drawer；N 新建连接", "good"));

  // global search (assets + commands)
  $("#globalSearch").addEventListener("keydown", (e)=>{
    if(e.key==="Enter"){
      openPalette($("#globalSearch").value.trim());
    }
  });

  // keyboard shortcuts
  window.addEventListener("keydown", (e)=>{
    if(e.ctrlKey && e.key.toLowerCase()==="k"){
      e.preventDefault();
      openPalette("");
      return;
    }
    if(e.altKey && e.key.toLowerCase()==="s"){ e.preventDefault(); toggleRightPane(); return; }
    if(e.altKey && e.key.toLowerCase()==="d"){ e.preventDefault(); toggleDrawer(); return; }
    if(e.ctrlKey && e.key === ","){ e.preventDefault(); toast("设置","占位设置入口", "good"); return; }
    if(e.key === "?" && !e.ctrlKey && !e.metaKey){ toast("帮助","Ctrl+K / Alt+S / Alt+D / N", "good"); }
    if(e.key.toLowerCase()==="n" && !e.ctrlKey && !e.metaKey && !isTypingTarget(e.target)){
      openConnModal({mode:"new"});
    }
    if(e.key === "Escape"){
      closeCtxMenu();
      closePalette();
      closeModal();
    }
  });
}
function isTypingTarget(t){
  return t && (t.tagName==="INPUT" || t.tagName==="TEXTAREA" || t.isContentEditable);
}
function toggleRightPane(){
  state.isRightPaneVisible = !state.isRightPaneVisible;
  $("#rightPane").classList.toggle("hidden", !state.isRightPaneVisible);
  pushHistory(`Toggle SFTP Pane: ${state.isRightPaneVisible ? "ON" : "OFF"}`);
  refreshSftp();
}
function toggleDrawer(){
  state.isDrawerVisible = !state.isDrawerVisible;
  $("#drawer").hidden = !state.isDrawerVisible;
  pushHistory(`Toggle Drawer: ${state.isDrawerVisible ? "ON" : "OFF"}`);
}

/* ===== Left rail ===== */
function setupRail(){
  $("#railToggleLeft").addEventListener("click", ()=>{
    state.isLeftCollapsed = !state.isLeftCollapsed;
    $("#assetSidebar").classList.toggle("collapsed", state.isLeftCollapsed);
    pushHistory(`Toggle Sidebar: ${state.isLeftCollapsed ? "collapsed" : "expanded"}`);
  });

  $("#railSessions").addEventListener("click", ()=>{
    state.viewMode = "sessions";
    setRailActive("sessions");
    renderWorkspace();
  });
  $("#railTree").addEventListener("click", ()=>{
    state.viewMode = "tree";
    setRailActive("tree");
    renderWorkspace();
  });
  $("#railNew").addEventListener("click", ()=>openConnModal({mode:"new"}));
  setRailActive("tree");
}
function setRailActive(mode){
  $("#railSessions").classList.toggle("active", mode==="sessions");
  $("#railTree").classList.toggle("active", mode==="tree");
}

/* ===== Assets ===== */
function setupAssets(){
  state.assets = mockAssets();
  // expand all by default
  state.assets.folders.forEach(f=>state.expandedFolderIds.add(f.id));

  $("#assetSearch").addEventListener("input", (e)=>{
    state.assetSearch = e.target.value.trim();
    renderAssets();
  });
  $("#btnExpandAll").addEventListener("click", ()=>{
    walkFolders(state.assets.folders, f=>state.expandedFolderIds.add(f.id));
    renderAssets();
  });
  $("#btnCollapseAll").addEventListener("click", ()=>{
    state.expandedFolderIds.clear();
    renderAssets();
  });

  // create dropdown
  const createMenu = $("#createMenu");
  $("#btnCreate").addEventListener("click", ()=>{
    createMenu.classList.toggle("open");
  });
  document.addEventListener("click", (e)=>{
    if(!e.target.closest(".Dropdown")) createMenu.classList.remove("open");
  });
  createMenu.addEventListener("click", (e)=>{
    const item = e.target.closest(".MenuItem");
    if(!item) return;
    createMenu.classList.remove("open");
    const action = item.dataset.action;
    if(action==="new-folder") createFolder();
    if(action==="new-conn") openConnModal({mode:"new"});
  });

  renderAssets();
}

function walkFolders(folders, fn){
  for(const f of folders){
    fn(f);
    const subFolders = (f.children||[]).filter(x=>x.type!=="conn");
    if(subFolders.length) walkFolders(subFolders, fn);
  }
}

function renderAssets(){
  const tree = $("#assetTree");
  tree.innerHTML = "";
  const hasAny = state.assets && state.assets.folders && state.assets.folders.length>0;
  $("#assetEmpty").hidden = hasAny;

  const q = state.assetSearch.toLowerCase();

  const renderFolder = (folder) => {
    // filter: show folder if it or any child matches
    const visible = folderMatches(folder, q);
    if(!visible) return null;

    const li = document.createElement("li");
    const row = document.createElement("div");
    row.className = "Node";
    row.dataset.id = folder.id;
    row.dataset.kind = "folder";
    row.innerHTML = `
      <span class="Twist">${state.expandedFolderIds.has(folder.id) ? "▾" : "▸"}</span>
      <span>📁</span>
      <span>${highlight(folder.name, q)}</span>
      <span class="badge">${countConns(folder)} </span>
    `;
    li.appendChild(row);

    const childrenUl = document.createElement("ul");
    childrenUl.className = "Children";
    childrenUl.hidden = !state.expandedFolderIds.has(folder.id);

    for(const child of (folder.children||[])){
      if(child.type==="conn"){
        const connVisible = connMatches(child, q);
        if(!connVisible) continue;
        const cli = document.createElement("li");
        const crow = document.createElement("div");
        crow.className = "Node";
        crow.dataset.id = child.id;
        crow.dataset.kind = "conn";
        crow.innerHTML = `
          <span class="Twist"></span>
          <span>🖥️</span>
          <span>${highlight(child.name, q)}</span>
          <span class="badge">${escapeHtml(child.user)}@${escapeHtml(child.host)}</span>
        `;
        cli.appendChild(crow);
        childrenUl.appendChild(cli);

        // double click to open
        crow.addEventListener("dblclick", ()=>{
          openConnection(child.id);
        });

        // selection
        crow.addEventListener("click", (e)=>selectAsset(e, child.id));
        crow.addEventListener("contextmenu", (e)=>{
          e.preventDefault();
          selectAsset(e, child.id, true);
          openCtxMenuForAsset(e.clientX, e.clientY, "conn");
        });

        // drag connection to folder
        crow.draggable = true;
        crow.addEventListener("dragstart", (e)=>{
          e.dataTransfer.setData("text/plain", JSON.stringify({type:"conn", id: child.id}));
        });
      }else{
        const node = renderFolder(child);
        if(node) childrenUl.appendChild(node);
      }
    }

    li.appendChild(childrenUl);

    row.addEventListener("click", (e)=>selectAsset(e, folder.id));
    row.addEventListener("dblclick", ()=>{
      const open = state.expandedFolderIds.has(folder.id);
      if(open) state.expandedFolderIds.delete(folder.id); else state.expandedFolderIds.add(folder.id);
      renderAssets();
    });
    row.addEventListener("contextmenu", (e)=>{
      e.preventDefault();
      selectAsset(e, folder.id, true);
      openCtxMenuForAsset(e.clientX, e.clientY, "folder");
    });

    // drop target
    row.addEventListener("dragover", (e)=>{
      e.preventDefault();
      row.style.outline="2px solid rgba(79,140,255,.35)";
    });
    row.addEventListener("dragleave", ()=>row.style.outline="");
    row.addEventListener("drop", (e)=>{
      e.preventDefault();
      row.style.outline="";
      try{
        const data = JSON.parse(e.dataTransfer.getData("text/plain"));
        if(data.type==="conn"){
          moveConnToFolder(data.id, folder.id);
        }
      }catch{}
    });

    return li;
  };

  for(const f of state.assets.folders){
    const node = renderFolder(f);
    if(node) tree.appendChild(node);
  }
}

function folderMatches(folder, q){
  if(!q) return true;
  if(folder.name.toLowerCase().includes(q)) return true;
  for(const child of (folder.children||[])){
    if(child.type==="conn"){
      if(connMatches(child, q)) return true;
    }else{
      if(folderMatches(child, q)) return true;
    }
  }
  return false;
}
function connMatches(conn, q){
  if(!q) return true;
  const hay = `${conn.name} ${conn.user}@${conn.host} ${conn.port}`.toLowerCase();
  return hay.includes(q);
}
function highlight(text, q){
  if(!q) return escapeHtml(text);
  const idx = text.toLowerCase().indexOf(q);
  if(idx<0) return escapeHtml(text);
  const a = escapeHtml(text.slice(0, idx));
  const b = escapeHtml(text.slice(idx, idx+q.length));
  const c = escapeHtml(text.slice(idx+q.length));
  return `${a}<span style="color:#cfe0ff">${b}</span>${c}`;
}
function countConns(folder){
  let n=0;
  for(const child of (folder.children||[])){
    if(child.type==="conn") n++;
    else n += countConns(child);
  }
  return n;
}

function selectAsset(e, id, keepIfSelected=false){
  const multi = e.ctrlKey || e.metaKey;
  const range = e.shiftKey;

  // For simplicity: ctrl toggles; shift not implemented fully (would need ordering).
  if(!multi && !range){
    if(keepIfSelected && state.selectedAssetIds.has(id) && state.selectedAssetIds.size===1){
      // keep
    }else{
      state.selectedAssetIds.clear();
      state.selectedAssetIds.add(id);
    }
  }else{
    if(state.selectedAssetIds.has(id)) state.selectedAssetIds.delete(id);
    else state.selectedAssetIds.add(id);
  }

  // re-render selected UI
  $$(".Node").forEach(n=>{
    n.classList.toggle("selected", state.selectedAssetIds.has(n.dataset.id));
  });
}

function findConnById(id){
  let found=null;
  const walk = (folders)=>{
    for(const f of folders){
      for(const c of (f.children||[])){
        if(c.type==="conn" && c.id===id) { found=c; return; }
        if(c.type!=="conn") walk([c]);
      }
      if(found) return;
    }
  };
  walk(state.assets.folders);
  return found;
}
function findFolderById(id){
  let found=null;
  const walk = (folders)=>{
    for(const f of folders){
      if(f.id===id){ found=f; return; }
      const subs=(f.children||[]).filter(x=>x.type!=="conn");
      if(subs.length) walk(subs);
      if(found) return;
    }
  };
  walk(state.assets.folders);
  return found;
}
function moveConnToFolder(connId, folderId){
  if(!connId || !folderId) return;
  const conn = removeConn(connId);
  if(!conn) return;
  const folder = findFolderById(folderId);
  if(!folder) return;
  folder.children = folder.children || [];
  folder.children.unshift(conn);
  state.expandedFolderIds.add(folderId);
  renderAssets();
  pushHistory(`Move connection "${conn.name}" -> folder "${folder.name}"`);
}
function removeConn(connId){
  let removed=null;
  const walk = (folders)=>{
    for(const f of folders){
      const idx = (f.children||[]).findIndex(x=>x.type==="conn" && x.id===connId);
      if(idx>=0){
        removed = f.children.splice(idx,1)[0];
        return true;
      }
      const subs = (f.children||[]).filter(x=>x.type!=="conn");
      for(const s of subs){
        if(walk([s])) return true;
      }
    }
    return false;
  };
  walk(state.assets.folders);
  return removed;
}

/* ===== Workspace / Tabs / Split ===== */
function renderWorkspace(){
  const body = $("#workspaceBody");
  body.innerHTML = "";

  if(state.viewMode === "sessions"){
    const all = getAllTabs();
    const pane = document.createElement("div");
    pane.className = "Pane focused";
    pane.innerHTML = `
      <div class="TabBar">
        <div class="Tab active"><span class="dot"></span><span>All Sessions</span></div>
      </div>
      <div class="TerminalArea">
        <div class="Terminal" style="font-family:var(--font);">
          <div style="color:rgba(231,235,244,.85);font-weight:700;margin-bottom:8px">已打开会话（All Sessions）</div>
          <div style="color:rgba(231,235,244,.70);font-size:12px;margin-bottom:10px">点击可切换；右键可关闭（示意）。</div>
          <div id="allSessionsList"></div>
        </div>
      </div>
    `;
    body.appendChild(pane);
    const list = $("#allSessionsList");
    list.innerHTML = all.length ? "" : `<div style="color:var(--muted)">暂无会话，去资产树双击连接打开。</div>`;
    for(const t of all){
      const btn = document.createElement("div");
      btn.className = "CtxItem";
      btn.style.borderRadius="12px";
      btn.style.marginBottom="6px";
      btn.textContent = `${t.title}  (${t.status})`;
      btn.addEventListener("click", ()=>{
        // activate in its pane
        activateTab(t.paneId, t.id);
        state.viewMode="tree";
        setRailActive("tree");
        renderWorkspace();
      });
      btn.addEventListener("contextmenu",(e)=>{
        e.preventDefault();
        openCtxMenu(e.clientX,e.clientY,[
          {label:"关闭该会话", run:()=>closeTab(t.paneId,t.id)},
          {label:"在新标签打开（复制）", run:()=>duplicateTab(t.paneId,t.id)},
        ]);
      });
      list.appendChild(btn);
    }
    return;
  }

  const panes = (state.splitMode === "single")
    ? ["left"]
    : ["left","right"];

  for(const paneId of panes){
    body.appendChild(renderPane(paneId));
  }
}

function renderPane(paneId){
  const pane = document.createElement("section");
  pane.className = "Pane" + (state.paneFocus===paneId ? " focused":"");
  pane.dataset.pane = paneId;

  pane.addEventListener("mousedown", ()=>{ state.paneFocus=paneId; renderWorkspace(); refreshSftp(); });

  const tabBar = document.createElement("div");
  tabBar.className = "TabBar";
  tabBar.addEventListener("contextmenu", (e)=>{
    // right click empty tabbar -> open palette?
    e.preventDefault();
    openCtxMenu(e.clientX, e.clientY, [
      {label:"新建连接", run:()=>openConnModal({mode:"new"})},
      {label:"打开命令面板", run:()=>openPalette("")},
      {sep:true},
      {label:"关闭全部标签", run:()=>closeAllTabs(paneId), disabled: state.panes[paneId].tabs.length===0},
    ]);
  });

  const tabs = state.panes[paneId].tabs;
  for(const t of tabs){
    const el = document.createElement("div");
    el.className = "Tab" + (t.id===state.panes[paneId].activeTabId ? " active":"");
    el.dataset.tab = t.id;
    el.dataset.status = t.status;
    el.draggable = true;
    el.innerHTML = `<span class="dot"></span><span>${escapeHtml(t.title)}</span><button class="x" title="关闭">✕</button>`;
    el.addEventListener("click", ()=>activateTab(paneId, t.id));
    el.querySelector(".x").addEventListener("click",(e)=>{e.stopPropagation(); closeTab(paneId, t.id);});
    el.addEventListener("contextmenu",(e)=>{
      e.preventDefault();
      openTabMenu(e.clientX,e.clientY,paneId,t.id);
    });

    // Drag to reorder / split
    el.addEventListener("dragstart",(e)=>{
      e.dataTransfer.setData("text/plain", JSON.stringify({type:"tab", paneId, tabId: t.id}));
      e.dataTransfer.effectAllowed = "move";
    });

    tabBar.appendChild(el);
  }

  // drop zones: allow dropping tabs to this tabbar (move)
  tabBar.addEventListener("dragover",(e)=>{ e.preventDefault(); });
  tabBar.addEventListener("drop",(e)=>{
    e.preventDefault();
    const data = safeJson(e.dataTransfer.getData("text/plain"));
    if(data?.type==="tab"){
      moveTab(data.paneId, paneId, data.tabId);
    }
  });

  const termArea = document.createElement("div");
  termArea.className = "TerminalArea";
  termArea.innerHTML = `
    <div class="Terminal" id="term-${paneId}">
      <div><span class="prompt">${paneId==="left"?"left":"right"}$</span> <span class="path">~</span> <span class="cmd">#</span> <span style="color:rgba(231,235,244,.75)">欢迎使用 HexHub Mock</span></div>
      <div style="color:rgba(231,235,244,.65);margin-top:6px">提示：双击左侧连接打开标签；拖动标签可分屏/移动；右键标签有批量关闭菜单；Ctrl+K 打开命令面板。</div>
      <div id="termLines-${paneId}" style="margin-top:10px"></div>
    </div>
    <div class="TermControls">
      <button class="Btn" data-act="run">执行</button>
      <input class="TermInput" data-act="input" placeholder="输入命令（mock）…" />
      <button class="Btn" data-act="clear">清屏</button>
    </div>
  `;

  // terminal actions
  const runBtn = termArea.querySelector('[data-act="run"]');
  const clrBtn = termArea.querySelector('[data-act="clear"]');
  const input = termArea.querySelector('[data-act="input"]');
  runBtn.addEventListener("click", ()=>runCommand(paneId, input.value));
  input.addEventListener("keydown",(e)=>{ if(e.key==="Enter"){ runCommand(paneId, input.value); }});
  clrBtn.addEventListener("click", ()=>{
    $("#termLines-"+paneId).innerHTML="";
    pushHistory(`Clear terminal (${paneId})`);
  });

  // Drag to split: drop tab into terminal area to create split and move into right pane if dropped on right half
  termArea.addEventListener("dragover",(e)=>{ e.preventDefault(); });
  termArea.addEventListener("drop",(e)=>{
    e.preventDefault();
    const data = safeJson(e.dataTransfer.getData("text/plain"));
    if(data?.type==="tab"){
      const rect = termArea.getBoundingClientRect();
      const toRight = (e.clientX - rect.left) > rect.width/2;
      if(state.splitMode==="single"){
        state.splitMode="split";
        // ensure right pane exists
        state.panes.right.tabs = state.panes.right.tabs || [];
        state.panes.right.activeTabId = state.panes.right.activeTabId || null;
      }
      const targetPane = (state.splitMode==="split" && toRight) ? "right" : "left";
      moveTab(data.paneId, targetPane, data.tabId);
      state.paneFocus = targetPane;
      renderWorkspace();
      refreshSftp();
      pushHistory(`Drag tab to ${targetPane} pane (split=${state.splitMode})`);
    }
  });

  pane.appendChild(tabBar);
  pane.appendChild(termArea);
  return pane;
}

function safeJson(s){ try{ return JSON.parse(s); }catch{ return null; } }

function getAllTabs(){
  const out = [];
  for(const pid of ["left","right"]){
    for(const t of state.panes[pid].tabs){
      out.push({...t, paneId: pid});
    }
  }
  return out;
}

function openConnection(connId){
  const conn = findConnById(connId);
  if(!conn) return;
  // if already opened in focus pane, activate; else open new
  const pid = state.splitMode==="split" ? state.paneFocus : "left";
  const existing = state.panes[pid].tabs.find(t=>t.connectionId===connId);
  if(existing){
    activateTab(pid, existing.id);
    toast("已打开", `已切换到 ${conn.name}`, "good");
    return;
  }
  const tab = {
    id: crypto.randomUUID(),
    connectionId: connId,
    title: conn.name,
    status: "connecting"
  };
  state.panes[pid].tabs.push(tab);
  state.panes[pid].activeTabId = tab.id;
  renderWorkspace();
  refreshActiveHint();
  pushHistory(`Open connection: ${conn.name} (${conn.user}@${conn.host}:${conn.port})`);

  // simulate connect
  setTimeout(()=>{
    tab.status = "connected";
    renderWorkspace();
    refreshActiveHint();
    refreshSftp();
  }, 600);
}

function activateTab(paneId, tabId){
  state.panes[paneId].activeTabId = tabId;
  state.paneFocus = paneId;
  renderWorkspace();
  refreshActiveHint();
  refreshSftp();
}

function closeTab(paneId, tabId){
  const p = state.panes[paneId];
  const idx = p.tabs.findIndex(t=>t.id===tabId);
  if(idx<0) return;
  const [removed] = p.tabs.splice(idx,1);
  pushHistory(`Close tab: ${removed.title}`);
  // pick new active
  if(p.activeTabId===tabId){
    const next = p.tabs[idx] || p.tabs[idx-1] || null;
    p.activeTabId = next ? next.id : null;
  }
  // if split and one pane empty -> merge back to single
  if(state.splitMode==="split"){
    const leftEmpty = state.panes.left.tabs.length===0;
    const rightEmpty = state.panes.right.tabs.length===0;
    if(leftEmpty && !rightEmpty){
      // move right->left
      state.panes.left.tabs = state.panes.right.tabs;
      state.panes.left.activeTabId = state.panes.right.activeTabId;
      state.panes.right.tabs = [];
      state.panes.right.activeTabId = null;
      state.splitMode = "single";
      state.paneFocus = "left";
    }else if(rightEmpty && !leftEmpty){
      state.splitMode = "single";
      state.paneFocus = "left";
    }else if(leftEmpty && rightEmpty){
      state.splitMode = "single";
      state.paneFocus = "left";
    }
  }
  renderWorkspace();
  refreshActiveHint();
  refreshSftp();
}

function duplicateTab(paneId, tabId){
  const p = state.panes[paneId];
  const t = p.tabs.find(x=>x.id===tabId);
  if(!t) return;
  const dup = {...t, id: crypto.randomUUID(), title: `${t.title} (2)`, status:"connecting"};
  p.tabs.push(dup);
  p.activeTabId = dup.id;
  renderWorkspace();
  refreshActiveHint();
  pushHistory(`Duplicate tab: ${t.title}`);
  setTimeout(()=>{ dup.status="connected"; renderWorkspace(); }, 400);
}

function closeAllTabs(paneId){
  state.panes[paneId].tabs = [];
  state.panes[paneId].activeTabId = null;
  pushHistory(`Close all tabs (${paneId})`);
  // handle merge
  if(state.splitMode==="split"){
    const other = paneId==="left" ? "right" : "left";
    if(state.panes[other].tabs.length>0){
      state.panes.left.tabs = state.panes[other].tabs;
      state.panes.left.activeTabId = state.panes[other].activeTabId;
    }else{
      state.panes.left.tabs = [];
      state.panes.left.activeTabId = null;
    }
    state.panes.right.tabs = [];
    state.panes.right.activeTabId = null;
    state.splitMode="single";
    state.paneFocus="left";
  }
  renderWorkspace();
  refreshActiveHint();
  refreshSftp();
}

function moveTab(fromPane, toPane, tabId){
  if(fromPane===toPane) return;
  const fp = state.panes[fromPane];
  const tp = state.panes[toPane];
  const idx = fp.tabs.findIndex(t=>t.id===tabId);
  if(idx<0) return;
  const [tab] = fp.tabs.splice(idx,1);
  tp.tabs.push(tab);
  tp.activeTabId = tab.id;
  if(fp.activeTabId===tabId){
    const next = fp.tabs[idx] || fp.tabs[idx-1] || null;
    fp.activeTabId = next ? next.id : null;
  }
  // ensure split if moving into right
  if(toPane==="right") state.splitMode="split";
}

/* ===== Tab context menu ===== */
function openTabMenu(x,y,paneId,tabId){
  const p = state.panes[paneId];
  const idx = p.tabs.findIndex(t=>t.id===tabId);
  const hasLeft = idx>0;
  const hasRight = idx<p.tabs.length-1;
  openCtxMenu(x,y,[
    {label:"关闭当前", run:()=>closeTab(paneId,tabId)},
    {label:"关闭左侧", run:()=>closeLeft(paneId, idx), disabled:!hasLeft},
    {label:"关闭右侧", run:()=>closeRight(paneId, idx), disabled:!hasRight},
    {label:"关闭其他", run:()=>closeOthers(paneId, idx), disabled:p.tabs.length<=1},
    {label:"关闭全部", run:()=>closeAllTabs(paneId), disabled:p.tabs.length===0},
    {sep:true},
    {label:"复制当前连接在新标签打开", run:()=>duplicateTab(paneId,tabId)},
  ]);
}
function closeLeft(paneId, idx){
  const p = state.panes[paneId];
  p.tabs.splice(0, idx);
  p.activeTabId = p.tabs[0]?.id || null;
  pushHistory(`Close left tabs (${paneId})`);
  renderWorkspace(); refreshSftp();
}
function closeRight(paneId, idx){
  const p = state.panes[paneId];
  p.tabs.splice(idx+1);
  p.activeTabId = p.tabs[idx]?.id || p.tabs.at(-1)?.id || null;
  pushHistory(`Close right tabs (${paneId})`);
  renderWorkspace(); refreshSftp();
}
function closeOthers(paneId, idx){
  const p = state.panes[paneId];
  const keep = p.tabs[idx];
  p.tabs = keep ? [keep] : [];
  p.activeTabId = keep ? keep.id : null;
  pushHistory(`Close other tabs (${paneId})`);
  renderWorkspace(); refreshSftp();
}

/* ===== Context menu generic ===== */
function openCtxMenu(x,y,items){
  const menu = $("#ctxMenu");
  menu.innerHTML = "";
  for(const it of items){
    if(it.sep){
      const sep = document.createElement("div");
      sep.className="CtxSep";
      menu.appendChild(sep);
      continue;
    }
    const row = document.createElement("div");
    row.className = "CtxItem" + (it.disabled ? " disabled":"");
    row.innerHTML = `<span>${escapeHtml(it.label)}</span><span style="color:var(--muted);font-size:12px">${escapeHtml(it.hint||"")}</span>`;
    row.addEventListener("click", ()=>{
      closeCtxMenu();
      if(!it.disabled) it.run?.();
    });
    menu.appendChild(row);
  }
  menu.hidden = false;
  const pad=10;
  const mw = 220;
  const mh = Math.min(360, items.length*38);
  const W = window.innerWidth;
  const H = window.innerHeight;
  menu.style.left = Math.min(W-mw-pad, x) + "px";
  menu.style.top = Math.min(H-mh-pad, y) + "px";
}
function closeCtxMenu(){ $("#ctxMenu").hidden = true; $("#ctxMenu").innerHTML=""; }
document.addEventListener("click", (e)=>{
  if(!e.target.closest(".CtxMenu")) closeCtxMenu();
});
document.addEventListener("contextmenu",(e)=>{
  // allow normal in inputs
  if(e.target.closest("input,textarea")) return;
  // otherwise we control our own menus; prevent default outside tree/terminal? optional
});

/* ===== Asset context menus ===== */
let clipboard = { mode:null, items:[] }; // mode: copy|cut
function openCtxMenuForAsset(x,y,kind){
  if(kind==="folder"){
    openCtxMenu(x,y,[
      {label:"新建目录", run:()=>createFolder()},
      {label:"新建连接", run:()=>openConnModal({mode:"new"})},
      {sep:true},
      {label:"粘贴", run:()=>pasteIntoSelectedFolder(), disabled: clipboard.items.length===0},
      {label:"重命名（示意）", run:()=>toast("重命名","此 mock 未实现内联重命名", "good")},
      {label:"删除（示意）", run:()=>toast("删除","此 mock 未实现删除文件夹", "bad")},
      {sep:true},
      {label:"刷新", run:()=>{ renderAssets(); toast("刷新","资产树已刷新", "good"); }},
    ]);
  }else{
    const multi = state.selectedAssetIds.size>1;
    openCtxMenu(x,y,[
      {label:"编辑", run:()=>openConnModal({mode:"edit"})},
      {label:"批量编辑", run:()=>toast("批量编辑","此 mock 仅展示入口", "good"), disabled: !multi},
      {label:"克隆", run:()=>cloneSelectedConn(), disabled: !hasSingleConnSelected()},
      {label:"复制 Host", run:()=>copyHostOfSelected(), disabled: !hasSingleConnSelected()},
      {sep:true},
      {label:"复制", run:()=>copySelected("copy")},
      {label:"剪切", run:()=>copySelected("cut")},
      {label:"粘贴", run:()=>pasteIntoSelectedFolder(), disabled: clipboard.items.length===0},
      {label:"删除（示意）", run:()=>toast("删除","此 mock 未实现删除连接", "bad")},
      {sep:true},
      {label:"刷新", run:()=>{ renderAssets(); toast("刷新","资产树已刷新", "good"); }},
      {label:"在新标签打开", run:()=>openSelectedConnInTab(), disabled: !hasSingleConnSelected()},
    ]);
  }
}

function selectedIds(){ return Array.from(state.selectedAssetIds); }
function getSelectedConnIds(){
  return selectedIds().filter(id=>!!findConnById(id));
}
function getSelectedFolderIds(){
  return selectedIds().filter(id=>!!findFolderById(id));
}
function hasSingleConnSelected(){ return getSelectedConnIds().length===1; }

function copySelected(mode){
  const ids = getSelectedConnIds();
  if(ids.length===0){ toast("提示","未选中连接", "bad"); return; }
  clipboard.mode = mode;
  clipboard.items = ids.map(id=>({type:"conn", id}));
  pushHistory(`${mode==="copy"?"Copy":"Cut"} ${ids.length} connection(s)`);
  toast("剪贴板", `${mode==="copy"?"复制":"剪切"}了 ${ids.length} 个连接`, "good");
}
function pasteIntoSelectedFolder(){
  const folderId = getSelectedFolderIds()[0];
  if(!folderId){ toast("提示","请先选中文件夹作为粘贴目标", "bad"); return; }
  for(const item of clipboard.items){
    if(item.type==="conn"){
      if(clipboard.mode==="copy"){
        const c = findConnById(item.id);
        if(c){
          const clone = {...c, id: crypto.randomUUID(), name: `${c.name} (Copy)`};
          const folder = findFolderById(folderId);
          folder.children.unshift(clone);
        }
      }else if(clipboard.mode==="cut"){
        moveConnToFolder(item.id, folderId);
      }
    }
  }
  if(clipboard.mode==="cut"){
    clipboard = {mode:null, items:[]};
  }
  renderAssets();
  pushHistory(`Paste into folder "${findFolderById(folderId).name}"`);
}
function cloneSelectedConn(){
  const id = getSelectedConnIds()[0];
  const c = findConnById(id);
  if(!c) return;
  const folder = findParentFolderOfConn(id);
  const clone = {...c, id: crypto.randomUUID(), name: `${c.name} (Copy)`};
  folder.children.unshift(clone);
  renderAssets();
  pushHistory(`Clone connection "${c.name}"`);
}
function findParentFolderOfConn(connId){
  let found=null;
  const walk = (folders)=>{
    for(const f of folders){
      if((f.children||[]).some(x=>x.type==="conn" && x.id===connId)){ found=f; return; }
      const subs=(f.children||[]).filter(x=>x.type!=="conn");
      if(subs.length) walk(subs);
      if(found) return;
    }
  };
  walk(state.assets.folders);
  return found || state.assets.folders[0];
}
async function copyHostOfSelected(){
  const id = getSelectedConnIds()[0];
  const c = findConnById(id);
  if(!c) return;
  const text = `${c.host}:${c.port}`;
  try{ await navigator.clipboard.writeText(text); toast("已复制", text, "good"); }
  catch{ toast("复制失败", text, "bad"); }
  pushHistory(`Copy host: ${text}`);
}
function openSelectedConnInTab(){
  const id = getSelectedConnIds()[0];
  if(id) openConnection(id);
}

/* ===== Create folder / Modal ===== */
function createFolder(){
  const name = "New Folder";
  const folder = { id: crypto.randomUUID(), name, children: [] };
  state.assets.folders.unshift(folder);
  state.expandedFolderIds.add(folder.id);
  renderAssets();
  pushHistory(`Create folder: ${name}`);
  toast("创建", "已创建文件夹（可继续实现重命名）", "good");
}

function openConnModal({mode}){
  // For mock: if edit and a single conn selected, load it; else new default
  const selectedConnId = getSelectedConnIds()[0];
  const editingConn = (mode==="edit" && selectedConnId) ? findConnById(selectedConnId) : null;

  const model = {
    id: editingConn?.id || null,
    color: "#4f8cff",
    name: editingConn?.name || "",
    env: editingConn?.env || state.env,
    host: editingConn?.host || "",
    port: editingConn?.port || 22,
    user: editingConn?.user || "root",
    auth: "password",
    password: "",
    remark: "",
    // proxy
    proxyType: "关闭",
    proxyHost: "127.0.0.1",
    proxyPort: 7890,
    proxyUser: "",
    proxyPass: "",
    proxyTimeout: 5,
    // advanced
    enableSftp: true,
    enableLrzsz: true,
    enableTrzsz: true,
    sftpSudo: false,
    x11Forward: false,
    termEnhance: false,
    pureTerm: false,
    recordLog: false,
    x11Display: "localhost:0.0",
    sftpCmd: "sudo -S /usr/lib/openssh/sftp-server",
    heartbeat: 0,
    connTimeout: 5,
    encoding: "UTF-8",
    termType: "xterm-256color",
    sftpPath: "/root",
    expire: "",
    initCmd: "",
    // env vars
    envVars: []
  };

  buildModalPanels(model);
  $("#modalOverlay").hidden = false;

  // modal tab switching
  $$(".ModalTab").forEach(btn=>{
    btn.onclick = ()=>{
      $$(".ModalTab").forEach(b=>b.classList.remove("active"));
      btn.classList.add("active");
      const tab = btn.dataset.tab;
      $("#mStandard").hidden = tab!=="standard";
      $("#mTunnel").hidden = tab!=="tunnel";
      $("#mProxy").hidden = tab!=="proxy";
      $("#mEnv").hidden = tab!=="env";
      $("#mAdvanced").hidden = tab!=="advanced";
    };
  });

  $("#modalClose").onclick = closeModal;
  $("#btnCancel").onclick = closeModal;

  $("#btnTestConn").onclick = ()=>{
    const host = $("#fHost").value.trim();
    const user = $("#fUser").value.trim();
    const port = parseInt($("#fPort").value,10);
    if(!host || !user || !port){
      toast("测试失败","host/port/user 必填", "bad");
      return;
    }
    toast("测试成功", `${user}@${host}:${port}（mock）`, "good");
    pushHistory(`Test connection: ${user}@${host}:${port}`);
  };

  $("#btnSaveConn").onclick = ()=>{
    // collect model from fields (minimal)
    const conn = {
      id: model.id || crypto.randomUUID(),
      type:"conn",
      name: $("#fName").value.trim() || "Unnamed",
      env: $("#fEnv").value,
      host: $("#fHost").value.trim(),
      port: parseInt($("#fPort").value,10) || 22,
      user: $("#fUser").value.trim() || "root",
    };
    if(!conn.host){
      toast("保存失败","Host 不能为空", "bad");
      return;
    }

    if(model.id){
      // update existing
      const c = findConnById(model.id);
      if(c){
        Object.assign(c, conn);
        toast("已保存","连接已更新", "good");
        pushHistory(`Save connection (edit): ${conn.name}`);
      }
    }else{
      // add to first folder or selected folder
      const targetFolder = findFolderById(getSelectedFolderIds()[0]) || state.assets.folders[0];
      targetFolder.children.unshift(conn);
      toast("已保存","已创建新连接", "good");
      pushHistory(`Save connection (new): ${conn.name}`);
    }
    closeModal();
    renderAssets();
  };

  // default tab to standard
  $$(".ModalTab").forEach(b=>b.classList.remove("active"));
  $(`.ModalTab[data-tab="standard"]`).classList.add("active");
  $("#mStandard").hidden=false;
  $("#mTunnel").hidden=true; $("#mProxy").hidden=true; $("#mEnv").hidden=true; $("#mAdvanced").hidden=true;
};

function closeModal(){ $("#modalOverlay").hidden = true; }

/* build modal content */
function buildModalPanels(model){
  // standard
  $("#mStandard").innerHTML = `
    <div class="FormGrid">
      <div class="Field" style="grid-column:1 / -1">
        <div class="Label">颜色标签</div>
        <div class="ColorDots" id="colorDots"></div>
      </div>
      <div class="Field">
        <div class="Label">名称</div>
        <input class="MiniInput" id="fName" value="${escapeHtml(model.name)}" placeholder="例如 Sharon" />
      </div>
      <div class="Field">
        <div class="Label">环境</div>
        <select class="MiniInput" id="fEnv">
          ${["生产","测试","开发"].map(v=>`<option ${v===model.env?"selected":""}>${v}</option>`).join("")}
        </select>
      </div>
      <div class="Field">
        <div class="Label">Host</div>
        <input class="MiniInput" id="fHost" value="${escapeHtml(model.host)}" placeholder="157.254.53.77" />
      </div>
      <div class="Field">
        <div class="Label">端口</div>
        <input class="MiniInput" id="fPort" value="${escapeHtml(model.port)}" />
      </div>
      <div class="Field">
        <div class="Label">User</div>
        <input class="MiniInput" id="fUser" value="${escapeHtml(model.user)}" />
      </div>
      <div class="Field">
        <div class="Label">认证方式</div>
        <div class="Pills" id="authPills"></div>
      </div>
      <div class="Field" style="grid-column:1 / -1">
        <div class="Label">备注</div>
        <textarea class="MiniInput TextArea" id="fRemark" placeholder="可选…">${escapeHtml(model.remark||"")}</textarea>
      </div>
    </div>
  `;

  const colors = ["#ff5f57","#ffbd2e","#28c840","#4f8cff","#a78bfa","#22c55e","#06b6d4","#f97316","#e879f9","#94a3b8"];
  const dots = $("#colorDots");
  dots.innerHTML = colors.map(c=>`<div class="Dot" data-c="${c}" style="background:${c}"></div>`).join("");
  $$(".Dot", dots).forEach(d=>{
    if(d.dataset.c===model.color) d.classList.add("active");
    d.addEventListener("click", ()=>{
      $$(".Dot", dots).forEach(x=>x.classList.remove("active"));
      d.classList.add("active");
      model.color = d.dataset.c;
    });
  });

  const auths = [
    {id:"password", label:"密码"},
    {id:"key", label:"私钥"},
    {id:"mfa", label:"MFA/2FA"},
    {id:"agent", label:"SSH Agent"},
    {id:"none", label:"不验证"},
  ];
  const authPills = $("#authPills");
  authPills.innerHTML = auths.map(a=>`<div class="Pill ${a.id===model.auth?"active":""}" data-a="${a.id}">${a.label}</div>`).join("");
  $$(".Pill", authPills).forEach(p=>{
    p.addEventListener("click", ()=>{
      $$(".Pill", authPills).forEach(x=>x.classList.remove("active"));
      p.classList.add("active");
      model.auth = p.dataset.a;
    });
  });

  // tunnel
  $("#mTunnel").innerHTML = `
    <div class="AssetEmpty">鼠标右键添加隧道（此 mock 仅做空态占位）</div>
  `;

  // proxy
  $("#mProxy").innerHTML = `
    <div class="FormGrid">
      <div class="Field">
        <div class="Label">代理方式</div>
        <select class="MiniInput" id="pType">
          ${["关闭","自动","SOCKS5","HTTP","HTTPS","SSH跳板"].map(v=>`<option ${v===model.proxyType?"selected":""}>${v}</option>`).join("")}
        </select>
      </div>
      <div class="Field">
        <div class="Label">连接超时(秒)</div>
        <input class="MiniInput" id="pTimeout" value="${escapeHtml(model.proxyTimeout)}" />
      </div>
      <div class="Field">
        <div class="Label">Host</div>
        <input class="MiniInput" id="pHost" value="${escapeHtml(model.proxyHost)}" />
      </div>
      <div class="Field">
        <div class="Label">端口</div>
        <input class="MiniInput" id="pPort" value="${escapeHtml(model.proxyPort)}" />
      </div>
      <div class="Field">
        <div class="Label">账号</div>
        <input class="MiniInput" id="pUser" value="${escapeHtml(model.proxyUser)}" />
      </div>
      <div class="Field">
        <div class="Label">密码</div>
        <input class="MiniInput" id="pPass" value="${escapeHtml(model.proxyPass)}" type="password" />
      </div>
    </div>
  `;

  // env vars
  $("#mEnv").innerHTML = `
    <div class="AssetEmpty">鼠标右键添加环境变量（此 mock 仅做空态占位）</div>
  `;

  // advanced
  $("#mAdvanced").innerHTML = `
    <div class="CheckRow">
      ${chk("enableSftp","启用 SFTP", model.enableSftp)}
      ${chk("enableLrzsz","启用 lrzsz", model.enableLrzsz)}
      ${chk("enableTrzsz","启用 trzsz", model.enableTrzsz)}
      ${chk("sftpSudo","SFTP-SUDO", model.sftpSudo)}
      ${chk("x11Forward","启用 X11 转发", model.x11Forward)}
      ${chk("termEnhance","终端增强模式", model.termEnhance)}
      ${chk("pureTerm","纯终端模式", model.pureTerm)}
      ${chk("recordLog","录制日志", model.recordLog)}
    </div>
    <div style="height:12px"></div>
    <div class="FormGrid">
      <div class="Field"><div class="Label">X11 Display</div><input class="MiniInput" value="${escapeHtml(model.x11Display)}"></div>
      <div class="Field"><div class="Label">SFTP 命令</div><input class="MiniInput" value="${escapeHtml(model.sftpCmd)}"></div>
      <div class="Field"><div class="Label">终端心跳时间(秒)</div><input class="MiniInput" value="${escapeHtml(model.heartbeat)}"></div>
      <div class="Field"><div class="Label">连接超时(秒)</div><input class="MiniInput" value="${escapeHtml(model.connTimeout)}"></div>
      <div class="Field"><div class="Label">编码</div><input class="MiniInput" value="${escapeHtml(model.encoding)}"></div>
      <div class="Field"><div class="Label">终端类型</div><input class="MiniInput" value="${escapeHtml(model.termType)}"></div>
      <div class="Field"><div class="Label">SFTP 默认路径</div><input class="MiniInput" value="${escapeHtml(model.sftpPath)}"></div>
      <div class="Field"><div class="Label">到期时间</div><input class="MiniInput" placeholder="留空表示不过期" value="${escapeHtml(model.expire)}"></div>
      <div class="Field" style="grid-column:1/-1"><div class="Label">初始执行命令</div><textarea class="MiniInput TextArea" placeholder="例如 export TERM=xterm-256color">${escapeHtml(model.initCmd)}</textarea></div>
    </div>
  `;
}
function chk(id,label,checked){
  return `<label class="Check"><input type="checkbox" ${checked?"checked":""} /> <span>${escapeHtml(label)}</span></label>`;
}

/* ===== Terminal actions (mock) ===== */
function runCommand(paneId, cmd){
  cmd = (cmd||"").trim();
  if(!cmd) return;
  const lines = $("#termLines-"+paneId);
  const active = getActiveConnectionForPane(paneId);
  const who = active ? `${active.user}@${active.host}:${active.port}` : "no-connection";
  const row = document.createElement("div");
  row.innerHTML = `<span class="prompt">${escapeHtml(who)}$</span> <span class="cmd">${escapeHtml(cmd)}</span>`;
  lines.appendChild(row);

  // fake output
  const out = document.createElement("div");
  out.style.color="rgba(231,235,244,.75)";
  out.style.margin="4px 0 10px";
  out.textContent = mockCommandOutput(cmd);
  lines.appendChild(out);

  // history
  pushHistory(`Run command (${paneId}): ${cmd}`);
  // clear input
  const input = document.querySelector(`[data-act="input"]`);
  if(input) input.value="";
  // autoscroll
  const term = $("#term-"+paneId);
  term.scrollTop = term.scrollHeight;
}
function mockCommandOutput(cmd){
  if(cmd.startsWith("ls")) return "bin  boot  dev  etc  home  lib  root  usr  var  (mock)";
  if(cmd.startsWith("pwd")) return "/root (mock)";
  if(cmd.includes("docker")) return "CONTAINER ID   IMAGE   STATUS   ... (mock)";
  return "ok (mock output)";
}

function getActiveConnectionForPane(paneId){
  const p = state.panes[paneId];
  const tab = p.tabs.find(t=>t.id===p.activeTabId);
  if(!tab) return null;
  return findConnById(tab.connectionId);
}

/* ===== SFTP mock ===== */
const mockFs = {
  "/root":[
    {name:".bashrc", mtime:"2026-02-10 15:55", type:"bashrc", size:"1.7KB"},
    {name:".ssh", mtime:"2026-02-04 17:56", type:"dir", size:"4KB"},
    {name:"go1.25.6.linux_amd64.tar.gz", mtime:"2026-01-16 02:30", type:"gz", size:"57MB"},
    {name:"tmp", mtime:"2025-12-05 23:11", type:"dir", size:"4KB"},
  ],
  "/root/.ssh":[
    {name:"authorized_keys", mtime:"2026-02-04 17:56", type:"file", size:"2.1KB"},
    {name:"config", mtime:"2026-02-04 17:56", type:"file", size:"0.6KB"},
  ],
  "/root/tmp":[
    {name:"build.log", mtime:"2026-03-04 12:03", type:"log", size:"88KB"},
    {name:"publish.zip", mtime:"2026-03-04 12:10", type:"zip", size:"14MB"},
  ]
};

function refreshSftp(){
  const pane = $("#rightPane");
  if(!state.isRightPaneVisible) return;
  const focus = state.splitMode==="split" ? state.paneFocus : "left";
  const activeConn = getActiveConnectionForPane(focus);
  const status = $("#sftpStatus");
  if(!activeConn){
    status.textContent = "未选择连接（切换标签后可联动）";
    $("#sftpTable").innerHTML = "";
    return;
  }
  status.textContent = `连接：${activeConn.name}  (${activeConn.user}@${activeConn.host})`;
  // list dir
  const path = $("#sftpPath").value.trim() || "/root";
  listDir(activeConn.id, path);
}

function listDir(connectionId, path){
  $("#sftpStatus").textContent += " · 加载中…";
  const rows = $("#sftpTable");
  rows.innerHTML = "";
  setTimeout(()=>{
    const items = (mockFs[path] || []);
    const q = ($("#sftpSearch").value||"").trim().toLowerCase();
    const filtered = q ? items.filter(it=>it.name.toLowerCase().includes(q)) : items;

    if(filtered.length===0){
      rows.innerHTML = `<tr><td colspan="4" style="color:var(--muted);padding:14px 6px">空目录或无匹配</td></tr>`;
      return;
    }
    for(const it of filtered){
      const tr = document.createElement("tr");
      tr.className = "FileRow";
      tr.innerHTML = `
        <td>${it.type==="dir"?"📁":"📄"} ${escapeHtml(it.name)}</td>
        <td>${escapeHtml(it.mtime)}</td>
        <td>${escapeHtml(it.type)}</td>
        <td class="num">${escapeHtml(it.size)}</td>
      `;
      tr.addEventListener("dblclick", ()=>{
        if(it.type==="dir"){
          const newPath = (path.endsWith("/")?path.slice(0,-1):path) + "/" + it.name;
          $("#sftpPath").value = newPath;
          pushHistory(`SFTP cd: ${newPath}`);
          refreshSftp();
        }
      });
      rows.appendChild(tr);
    }
  }, 450);
}

function setupSftpControls(){
  $("#btnSftpRefresh").addEventListener("click", ()=>{ pushHistory("SFTP refresh"); refreshSftp(); });
  $("#sftpSearch").addEventListener("input", ()=>refreshSftp());
  $("#sftpPath").addEventListener("keydown",(e)=>{ if(e.key==="Enter"){ refreshSftp(); pushHistory(`SFTP path: ${$("#sftpPath").value}`);} });
  $("#btnSftpUp").addEventListener("click", ()=>{
    const p = $("#sftpPath").value.trim() || "/root";
    if(p==="/") return;
    const up = p.replace(/\/+$/,"").split("/").slice(0,-1).join("/") || "/";
    $("#sftpPath").value = up;
    pushHistory(`SFTP up: ${up}`);
    refreshSftp();
  });
}

/* ===== Drawer: Snippets & History ===== */
function setupDrawer(){
  // tabs
  $$(".DrawerTab").forEach(btn=>{
    btn.addEventListener("click", ()=>{
      $$(".DrawerTab").forEach(b=>b.classList.remove("active"));
      btn.classList.add("active");
      const tab = btn.dataset.tab;
      $("#panelSnippets").hidden = tab!=="snippets";
      $("#panelHistory").hidden = tab!=="history";
    });
  });

  $("#btnClearHistory").addEventListener("click", ()=>{
    state.history = [];
    renderHistory();
    toast("History","已清空", "good");
  });

  $("#snippetSearch").addEventListener("input", renderSnippets);

  $("#btnNewSnippet").addEventListener("click", ()=>{
    const title = prompt("Snippet 标题：", "new snippet");
    if(!title) return;
    const content = prompt("Snippet 内容：", "echo hello");
    if(content==null) return;
    state.snippets.unshift({ id: crypto.randomUUID(), vaultId: state.activeVaultId, title, content });
    renderSnippets();
    toast("Snippets","已新增", "good");
    pushHistory(`Add snippet: ${title}`);
  });

  renderVaults();
  renderSnippets();
  renderHistory();
}

function renderVaults(){
  const box = $("#vaults");
  box.innerHTML = "";
  for(const v of state.vaults){
    const el = document.createElement("div");
    el.className = "VaultItem" + (v.id===state.activeVaultId ? " active":"");
    el.textContent = v.name;
    el.addEventListener("click", ()=>{
      state.activeVaultId = v.id;
      renderVaults();
      renderSnippets();
    });
    box.appendChild(el);
  }
}
function renderSnippets(){
  const box = $("#snippets");
  const q = ($("#snippetSearch").value||"").trim().toLowerCase();
  box.innerHTML = "";
  const items = state.snippets.filter(s=>s.vaultId===state.activeVaultId)
    .filter(s=> !q || (s.title.toLowerCase().includes(q) || s.content.toLowerCase().includes(q)));

  if(items.length===0){
    box.innerHTML = `<div style="color:var(--muted);padding:10px">暂无 snippets</div>`;
    return;
  }
  for(const s of items){
    const card = document.createElement("div");
    card.className = "SnippetCard";
    card.innerHTML = `
      <div class="SnippetTitle">${escapeHtml(s.title)}</div>
      <div class="SnippetCode">${escapeHtml(s.content)}</div>
      <div class="SnippetActions">
        <button class="Btn" data-act="copy">复制</button>
        <button class="Btn primary" data-act="insert">插入到终端</button>
      </div>
    `;
    card.querySelector('[data-act="copy"]').addEventListener("click", async ()=>{
      try{ await navigator.clipboard.writeText(s.content); toast("已复制", s.title, "good"); }
      catch{ toast("复制失败", s.title, "bad"); }
      pushHistory(`Copy snippet: ${s.title}`);
    });
    card.querySelector('[data-act="insert"]').addEventListener("click", ()=>{
      const pane = state.splitMode==="split" ? state.paneFocus : "left";
      const input = document.querySelector(`#workspaceBody .Pane[data-pane="${pane}"] .TermInput`);
      if(input){ input.value = s.content; input.focus(); }
      toast("已插入", `插入到 ${pane} 输入框`, "good");
      pushHistory(`Insert snippet: ${s.title}`);
    });
    box.appendChild(card);
  }
}
function renderHistory(){
  const box = $("#historyList");
  box.innerHTML = "";
  if(state.history.length===0){
    box.innerHTML = `<div style="color:var(--muted);padding:10px">暂无历史</div>`;
    return;
  }
  for(const h of state.history){
    const el = document.createElement("div");
    el.className = "HistItem";
    el.innerHTML = `<div class="HistTime">${escapeHtml(h.time)}</div><div class="HistText">${escapeHtml(h.text)}</div>`;
    box.appendChild(el);
  }
}

/* ===== Command Palette ===== */
function openPalette(prefill=""){
  $("#paletteOverlay").hidden = false;
  const input = $("#paletteInput");
  input.value = prefill;
  input.focus();
  renderPaletteList(prefill);
}
function closePalette(){ $("#paletteOverlay").hidden = true; }
function renderPaletteList(q){
  const list = $("#paletteList");
  const qq = (q||"").toLowerCase().trim();
  const items = commands.filter(c=>!qq || c.title.toLowerCase().includes(qq));
  list.innerHTML = "";
  for(const c of items){
    const row = document.createElement("div");
    row.className = "PaletteItem";
    row.innerHTML = `<div>${escapeHtml(c.title)}</div><div class="hint">${escapeHtml(c.hint||"")}</div>`;
    row.addEventListener("click", ()=>{
      closePalette();
      c.run();
    });
    list.appendChild(row);
  }
  if(items.length===0){
    list.innerHTML = `<div style="padding:12px;color:var(--muted)">无匹配命令</div>`;
  }
}
function setupPalette(){
  $("#paletteOverlay").addEventListener("click",(e)=>{
    if(e.target.id==="paletteOverlay") closePalette();
  });
  $("#paletteInput").addEventListener("input",(e)=>renderPaletteList(e.target.value));
  $("#paletteInput").addEventListener("keydown",(e)=>{
    if(e.key==="Enter"){
      const first = commands.find(c=>c.title.toLowerCase().includes(e.target.value.toLowerCase().trim()));
      if(first){ closePalette(); first.run(); }
    }
  });
}

/* ===== Active hint ===== */
function refreshActiveHint(){
  const focus = state.splitMode==="split" ? state.paneFocus : "left";
  const conn = getActiveConnectionForPane(focus);
  $("#activeHint").textContent = conn ? `${conn.name} · ${conn.user}@${conn.host}:${conn.port}` : "—";
  $("#envPill").textContent = conn?.env || state.env;
}

/* ===== Boot ===== */
function boot(){
  setupSplitters();
  setupTopbar();
  setupRail();
  setupAssets();
  setupSftpControls();
  setupDrawer();
  setupPalette();

  // workspace initial
  renderWorkspace();
  refreshActiveHint();
  refreshSftp();

  // view tabs in workspace top (optional)
  $("#viewTerm").addEventListener("click", ()=>{
    $("#viewTerm").classList.add("active");
    $("#viewSessionsTab").classList.remove("active");
    state.viewMode="tree";
    setRailActive("tree");
    renderWorkspace();
  });
  $("#viewSessionsTab").addEventListener("click", ()=>{
    $("#viewSessionsTab").classList.add("active");
    $("#viewTerm").classList.remove("active");
    state.viewMode="sessions";
    setRailActive("sessions");
    renderWorkspace();
  });

  // close ctx on scroll/resize
  window.addEventListener("resize", closeCtxMenu);
  document.addEventListener("scroll", closeCtxMenu, true);
}
boot();