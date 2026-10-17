using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.ComponentModel;
using System.Web.Script.Serialization;
namespace R6ConfigBridge {
 public class MainWindow:Form {
  readonly ComboBox intl=new ComboBox(),cn=new ComboBox(),direction=new ComboBox(),category=new ComboBox();
  readonly TextBox search=new TextBox();readonly CheckBox different=new CheckBox(),details=new CheckBox();
  readonly Label cnScan=new Label(),intlScan=new Label();readonly Button scan=new Button();Button applyButton,previewButton;
  readonly DataGridView grid=new DataGridView();readonly Label status=new Label(),notice=new Label();
  readonly string backupRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backups");
  Snapshot left,right;bool loading=false;
  public MainWindow(){
   Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
   Text="R6 配置迁移工具 · MVP 0.2";Width=1240;Height=820;MinimumSize=new Size(1000,650);StartPosition=FormStartPosition.CenterScreen;
   Font=new Font("Microsoft YaHei UI",9);BackColor=Color.FromArgb(246,247,250);
   TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(18)};
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));root.RowStyles.Add(new RowStyle(SizeType.Absolute,130));root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));root.RowStyles.Add(new RowStyle(SizeType.Absolute,46));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));Controls.Add(root);
   root.Controls.Add(new Label{Text="R6 配置迁移  /  国际服 ⇄ 国服",Font=new Font(Font.FontFamily,18,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
   TableLayoutPanel paths=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
   paths.Controls.Add(ProfileGroup("国服 · CN",cn),0,0);paths.Controls.Add(ProfileGroup("国际服",intl),1,0);
   foreach(ComboBox cb in new[]{intl,cn}){cb.Dock=DockStyle.Fill;cb.DropDownStyle=ComboBoxStyle.DropDownList;cb.FormattingEnabled=true;cb.Format+=delegate(object sender,ListControlConvertEventArgs e){e.Value=details.Checked?e.ListItem.ToString():"账号配置 "+(cb.Items.IndexOf(e.ListItem)+1);};cb.SelectedIndexChanged+=delegate{if(!loading)Reload();};}
   root.Controls.Add(paths,0,1);
   notice.Dock=DockStyle.Fill;notice.ForeColor=Color.FromArgb(137,75,0);notice.Text="仅在退出两服后应用。启动器的云同步可能覆盖本地设置，游戏内效果须另行核对。\n具体动作键位（含鼠标侧键绑定）尚未解析，本版不会修改二进制存档。内部硬件、设备 ID、网络字段只读。";root.Controls.Add(notice,0,2);
   FlowLayoutPanel filters=new FlowLayoutPanel{Dock=DockStyle.Fill};
   category.DropDownStyle=ComboBoxStyle.DropDownList;category.Width=145;category.Items.Add("全部类别");foreach(string g in Catalog.Fields.Select(f=>f.Group).Distinct())category.Items.Add(g);category.Items.Add("内部字段（只读）");category.SelectedIndex=0;category.SelectedIndexChanged+=delegate{Filter();};
   search.Width=155;search.TextChanged+=delegate{Filter();};different.Text="只看差异";different.AutoSize=true;different.CheckedChanged+=delegate{Filter();};
   details.Text="技术详情";details.AutoSize=true;details.CheckedChanged+=delegate{foreach(DataGridViewRow row in grid.Rows)row.Cells[2].Value=UiText.RowName((string)row.Tag,details.Checked);intl.Refresh();cn.Refresh();Filter();};filters.Controls.Add(details);filters.Controls.Add(category);filters.Controls.Add(new Label{Text="搜索",AutoSize=true,Padding=new Padding(0,6,0,0)});filters.Controls.Add(search);filters.Controls.Add(different);scan.Text="扫描已安装游戏";scan.AutoSize=true;scan.Click+=delegate{ScanInstalled();};filters.Controls.Add(scan);root.Controls.Add(filters,0,3);
   grid.Dock=DockStyle.Fill;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AllowUserToResizeRows=false;grid.RowHeadersVisible=false;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;
   grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Pick",HeaderText="选择",FillWeight=40});
   foreach(string h in new[]{"类别","配置项","国际服值","国服值","状态"}){int index=grid.Columns.Add(h,h);grid.Columns[index].ReadOnly=true;}
   grid.Columns[4].DisplayIndex=3;grid.Columns[1].FillWeight=95;grid.Columns[2].FillWeight=300;grid.Columns[3].FillWeight=85;grid.Columns[4].FillWeight=85;grid.Columns[5].FillWeight=95;
   grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
   grid.CellValueChanged+=delegate{UpdateStatus();};root.Controls.Add(grid,0,4);
   FlowLayoutPanel select=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0)};
   select.Controls.Add(Button("勾选可见差异",delegate{Choose(true,true);}));select.Controls.Add(Button("勾选可见全部",delegate{Choose(true,false);}));select.Controls.Add(Button("清空勾选",delegate{foreach(DataGridViewRow row in grid.Rows)row.Cells[0].Value=false;}));
   select.Controls.Add(new Label{Text="提示：同步 ADS 时请同时选择开关、乘数和所需倍率；数值保持原样；技术详情仅显示标识，不更改设置。",AutoSize=true,Padding=new Padding(6,7,0,0)});root.Controls.Add(select,0,5);
   FlowLayoutPanel actions=new FlowLayoutPanel{Dock=DockStyle.Fill};direction.DropDownStyle=ComboBoxStyle.DropDownList;direction.Items.AddRange(new object[]{"国际服 → 国服","国服 → 国际服"});direction.SelectedIndex=0;direction.Width=170;
   actions.Controls.Add(direction);previewButton=Button("预览所选修改",delegate{Preview(false);});applyButton=Button("应用（先备份）",delegate{Preview(true);});actions.Controls.Add(previewButton);actions.Controls.Add(applyButton);actions.Controls.Add(Button("恢复备份…",delegate{Restore();}));actions.Controls.Add(Button("打开备份目录",delegate{Directory.CreateDirectory(backupRoot);Process.Start("explorer.exe",backupRoot);}));root.Controls.Add(actions,0,6);
   status.Dock=DockStyle.Fill;status.ForeColor=Color.FromArgb(60,70,90);root.Controls.Add(status,0,7);Discover();
  }
  GroupBox ProfileGroup(string title,ComboBox cb){
   GroupBox box=new GroupBox{Text=title,Dock=DockStyle.Fill,Padding=new Padding(10)};
   TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));layout.Controls.Add(cb,0,0);layout.Controls.Add(Button("手动选择",delegate{Browse(cb);}),1,0);Label result=cb==cn?cnScan:intlScan;result.Dock=DockStyle.Fill;result.Text="尚未扫描安装信息；可扫描或手动选择配置。";layout.Controls.Add(result,0,1);layout.SetColumnSpan(result,2);box.Controls.Add(layout);return box;
  }
  Button Button(string text,Action action){Button b=new Button{Text=text,AutoSize=true,Height=30,FlatStyle=FlatStyle.System};b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"操作未执行／未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};return b;}
  void Browse(ComboBox cb){using(OpenFileDialog d=new OpenFileDialog{Filter="GameSettings.ini|GameSettings.ini",Title="选择对应账号的 GameSettings.ini"})if(d.ShowDialog(this)==DialogResult.OK){if(!string.Equals(Path.GetFileName(d.FileName),"GameSettings.ini",StringComparison.OrdinalIgnoreCase))throw new Exception("只能选择 GameSettings.ini。");if(!cb.Items.Contains(d.FileName))cb.Items.Add(d.FileName);cb.SelectedItem=d.FileName;}}
  void Populate(ComboBox cb,string root){string previous=cb.SelectedItem as string;cb.Items.Clear();if(Directory.Exists(root))foreach(string d in Directory.GetDirectories(root)){string path=Path.Combine(d,"GameSettings.ini");if(File.Exists(path))cb.Items.Add(path);}if(previous!=null&&cb.Items.Contains(previous))cb.SelectedItem=previous;else if(cb.Items.Count==1)cb.SelectedIndex=0;}
  void Discover(){loading=true;try{string docs=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);Populate(intl,Path.Combine(docs,"My Games","Rainbow Six - Siege"));Populate(cn,Path.Combine(docs,"My Games","彩虹六号：攻势"));}finally{loading=false;}Reload();}
  void ScanInstalled(){
   if(!scan.Enabled)return;scan.Enabled=false;scan.Text="扫描中…";cnScan.Text=intlScan.Text="正在扫描已知配置位置和平台记录…";
   BackgroundWorker worker=new BackgroundWorker();worker.DoWork+=delegate(object sender,DoWorkEventArgs e){e.Result=Discovery.Scan();};
   worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){try{if(IsDisposed)return;if(e.Error!=null){cnScan.Text=intlScan.Text="扫描错误：结果不可用，请手动选择。";}else UseScan((ScanResult)e.Result);}finally{if(!IsDisposed){scan.Enabled=true;scan.Text="扫描已安装游戏";}worker.Dispose();}};worker.RunWorkerAsync();
  }
  void UseScan(ScanResult result){
   loading=true;try{UseConfigs(intl,result.International.Configs);UseConfigs(cn,result.China.Configs);intlScan.Text=result.International.ToString();cnScan.Text=result.China.ToString();}finally{loading=false;}Reload();
  }
  void UseConfigs(ComboBox cb,string[] paths){string selected=cb.SelectedItem as string;cb.Items.Clear();foreach(string path in paths)if(!cb.Items.Contains(path))cb.Items.Add(path);if(selected!=null&&File.Exists(selected)){if(!cb.Items.Contains(selected))cb.Items.Add(selected);cb.SelectedItem=selected;}else if(cb.Items.Count==1)cb.SelectedIndex=0;}
  void Reload(){
   grid.Rows.Clear();left=right=null;applyButton.Enabled=previewButton.Enabled=false;
   try{
    if(intl.SelectedItem!=null)left=Snapshot.Load((string)intl.SelectedItem);if(cn.SelectedItem!=null)right=Snapshot.Load((string)cn.SelectedItem);
    if(left==null&&right==null){status.Text="请选择两服账号配置；多账号不会自动猜选。可使用“手动选择”定位其他安装／文档路径。";return;}
    applyButton.Enabled=previewButton.Enabled=left!=null&&right!=null;SortedSet<string> ids=new SortedSet<string>();if(left!=null)ids.UnionWith(left.Ini.Values.Keys);if(right!=null)ids.UnionWith(right.Ini.Values.Keys);
    foreach(string id in ids){Field f=Catalog.Fields.Find(x=>x.Id==id);string l=left==null?"（未加载）":left.Ini.Values.ContainsKey(id)?left.Ini.Values[id]:"（缺失）",r=right==null?"（未加载）":right.Ini.Values.ContainsKey(id)?right.Ini.Values[id]:"（缺失）";bool safe=f!=null&&left!=null&&right!=null&&left.Ini.Values.ContainsKey(id)&&right.Ini.Values.ContainsKey(id);
     int n=grid.Rows.Add(false,f!=null?f.Group:"内部字段（只读）",UiText.RowName(id,details.Checked),l,r,!safe?"只读 / 不支持":l==r?"相同":"不同");DataGridViewRow row=grid.Rows[n];row.Tag=id;row.Cells[0].ReadOnly=!safe;
     if(!safe){row.DefaultCellStyle.ForeColor=Color.Gray;row.Cells[0].Style.BackColor=Color.FromArgb(235,235,235);}else if(l!=r)row.DefaultCellStyle.BackColor=Color.FromArgb(255,248,225);
    }Filter();
   }catch(Exception e){status.Text="读取失败："+e.Message;}
  }
  void Filter(){grid.CurrentCell=null;foreach(DataGridViewRow row in grid.Rows){string g=Convert.ToString(row.Cells[1].Value),s=Convert.ToString(row.Cells[2].Value);row.Visible=(details.Checked||(Catalog.Allowed((string)row.Tag)&&UiText.Name((string)row.Tag)!=null))&&(category.SelectedIndex==0||g==Convert.ToString(category.SelectedItem))&&(!different.Checked||Convert.ToString(row.Cells[3].Value)!=Convert.ToString(row.Cells[4].Value))&&(search.Text.Length==0||s.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0);}UpdateStatus();}
  void Choose(bool pick,bool onlyDiff){foreach(DataGridViewRow row in grid.Rows)if(row.Visible&&!row.Cells[0].ReadOnly&&(!onlyDiff||Convert.ToString(row.Cells[3].Value)!=Convert.ToString(row.Cells[4].Value)))row.Cells[0].Value=pick;}
  string[] Picks(){grid.EndEdit();return grid.Rows.Cast<DataGridViewRow>().Where(r=>Convert.ToBoolean(r.Cells[0].Value)&&!r.Cells[0].ReadOnly).Select(r=>(string)r.Tag).ToArray();}
  void UpdateStatus(){int selected=grid.Rows.Cast<DataGridViewRow>().Count(r=>Convert.ToBoolean(r.Cells[0].Value));int available=grid.Rows.Cast<DataGridViewRow>().Count(r=>!r.Cells[0].ReadOnly);status.Text=(left==null||right==null?"仅加载单服，暂不能迁移。 ":"")+"已加载 "+grid.Rows.Count+" 项 / 可选择 "+available+" 项 / 已勾选 "+selected+" 项（包括被筛选隐藏的勾选项）。默认不自动修改任何配置。";}
  string PreviewText(){
   if(left==null||right==null)throw new Exception("请先选择两份配置。");Snapshot src=direction.SelectedIndex==0?left:right,dst=direction.SelectedIndex==0?right:left;string[] ids=Picks();dst.Ini.Patch(src.Ini,ids);
   List<string> changed=ids.Where(id=>src.Ini.Values[id]!=dst.Ini.Values[id]).ToList();if(changed.Count==0)throw new Exception("所选项目没有差异。");
   StringBuilder text=new StringBuilder();text.AppendLine(Convert.ToString(direction.SelectedItem));if(details.Checked){text.AppendLine("来源："+src.Path);text.AppendLine("目标："+dst.Path);}text.AppendLine();foreach(string id in changed)text.AppendLine(UiText.RowName(id,details.Checked)+" : "+dst.Ini.Values[id]+" → "+src.Ini.Values[id]);
   text.AppendLine();text.AppendLine("只修改以上 "+changed.Count+" 个差异字段，其他字段、注释和换行保留。具体动作键位不包含在内。云同步和游戏内生效需另行验证。");return text.ToString();
  }
  void Preview(bool apply){
   string text=PreviewText();Snapshot src=direction.SelectedIndex==0?left:right,dst=direction.SelectedIndex==0?right:left;string[] ids=Picks();
   using(Form dialog=new Form{Text=apply?"确认迁移（目标将先备份）":"修改预览",Width=820,Height=560,StartPosition=FormStartPosition.CenterParent,Font=Font}){
    TextBox t=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,Dock=DockStyle.Fill,Text=text.ToString(),WordWrap=false};Panel buttons=new Panel{Dock=DockStyle.Bottom,Height=50};Button ok=new Button{Text=apply?"确认应用":"关闭",Width=130,Left=640,Top=10,DialogResult=DialogResult.OK};Button cancel=new Button{Text="取消",Width=100,Left=520,Top=10,DialogResult=DialogResult.Cancel};buttons.Controls.Add(ok);if(apply)buttons.Controls.Add(cancel);dialog.Controls.Add(t);dialog.Controls.Add(buttons);dialog.AcceptButton=ok;dialog.CancelButton=cancel;
    if(dialog.ShowDialog(this)!=DialogResult.OK||!apply)return;
   }
   string backup=Store.Apply(src,dst,ids,backupRoot,Store.GameRunning);Reload();MessageBox.Show(this,"配置文件已写入并回读校验。\n备份："+backup+"\n\n请启动目标服核对实际效果。","本地迁移完成",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  void Restore(){
   if(!Directory.Exists(backupRoot))throw new Exception("还没有迁移备份。");
   using(OpenFileDialog d=new OpenFileDialog{InitialDirectory=backupRoot,Filter="迁移工具备份|*.ini",Title="选择要恢复的备份（保留同名 .xml）"})if(d.ShowDialog(this)==DialogResult.OK){BackupInfo info=Store.ReadBackup(d.FileName);if(left==null||right==null||(!string.Equals(info.Target,left.Path,StringComparison.OrdinalIgnoreCase)&&!string.Equals(info.Target,right.Path,StringComparison.OrdinalIgnoreCase)))throw new Exception("备份目标不是当前所选的两服配置。");if(MessageBox.Show(this,"恢复到：\n"+info.Target+"\n\n恢复前也会备份当前内容。目标若已被游戏或其他软件修改，将拒绝覆盖。","确认恢复",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)==DialogResult.OK){Store.Restore(d.FileName,info.Target,backupRoot,Store.GameRunning);Reload();MessageBox.Show(this,"恢复完成，回读校验通过。");}}
  }
  public object Smoke(string dir){
   Directory.CreateDirectory(dir);Show();Application.DoEvents();ScanResult scanResult=Discovery.Scan();UseScan(scanResult);Application.DoEvents();
   if(details.Checked||grid.Columns[4].DisplayIndex>=grid.Columns[3].DisplayIndex||cn.Parent.Parent.Left>=intl.Parent.Parent.Left)throw new Exception("Default UI layout mismatch");
   foreach(DataGridViewRow row in grid.Rows)if(row.Visible){string id=(string)row.Tag;if(!Catalog.Allowed(id)||Convert.ToString(row.Cells[2].Value).Contains(id.Split('/')[1]))throw new Exception("Raw identifier exposed by default");}
   int visible=grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible);int n=grid.Rows.Count,available=grid.Rows.Cast<DataGridViewRow>().Count(r=>!r.Cells[0].ReadOnly);if(n==0||available==0)throw new Exception("No real configurations loaded");
   Choose(true,true);int selected=Picks().Length;if(selected==0)throw new Exception("No differences selected");different.Checked=true;search.Text="鼠标";Application.DoEvents();int filtered=grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible);search.Clear();different.Checked=false;
   direction.SelectedIndex=1;string[] picks=Picks();byte[] preview=left.Ini.Patch(right.Ini,picks);direction.SelectedIndex=0;
   using(Bitmap image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save(Path.Combine(dir,"window-smoke.png"));}
   Snapshot[] originals={left,right};List<object> sandbox=new List<object>();foreach(int index in new[]{0,1}){
    Snapshot source=originals[index],target=originals[1-index];string fixture=Path.Combine(dir,"direction-"+index);Directory.CreateDirectory(fixture);string sp=Path.Combine(fixture,"source.ini"),tp=Path.Combine(fixture,"GameSettings.ini");File.WriteAllBytes(sp,source.Bytes);File.WriteAllBytes(tp,target.Bytes);
    string[] safe=Catalog.Fields.Select(f=>f.Id).Where(id=>source.Ini.Values.ContainsKey(id)&&target.Ini.Values.ContainsKey(id)).ToArray();string backup=Store.Apply(Snapshot.Load(sp),Snapshot.Load(tp),safe,Path.Combine(fixture,"backups"),delegate{return false;});
    Ini after=new Ini(File.ReadAllBytes(tp));foreach(string id in safe)if(after.Values[id]!=source.Ini.Values[id])throw new Exception("Sandbox mismatch "+id);
    foreach(string id in target.Ini.Values.Keys)if(!safe.Contains(id)&&after.Values[id]!=target.Ini.Values[id])throw new Exception("Protected field changed "+id);
    Store.Restore(backup,tp,Path.Combine(fixture,"backups"),delegate{return false;});if(Store.Hash(File.ReadAllBytes(tp))!=target.Hash)throw new Exception("Restore mismatch");sandbox.Add(new{Direction=index,Fields=safe.Length,Restored=true});
   }
   foreach(Snapshot snap in originals)if(Store.Hash(File.ReadAllBytes(snap.Path))!=snap.Hash)throw new Exception("Real config changed during smoke; possible game write");
   object result=new{CNFirst=true,DetailsDefaultOff=!details.Checked,DefaultVisibleRows=visible,InternationalScan=scanResult.International.ToString(),CNScan=scanResult.China.ToString(),Rows=n,Available=available,SelectedDifferences=selected,MouseFilteredRows=filtered,ReversePreviewBytes=preview.Length,GameRunning=Store.GameRunning(),RealConfigsUntouched=true,Sandbox=sandbox};
   string json=new JavaScriptSerializer().Serialize(result);File.WriteAllText(Path.Combine(dir,"smoke-result.json"),json,Encoding.UTF8);Close();return result;
  }
 }
 static class Program {
  [STAThread]static void Main(string[] args){
   string smokeSuffix=args.Length==2&&args[0]=="--smoke"?"-Smoke-"+Store.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(args[1]))):"";bool created;using(Mutex mutex=new Mutex(true,"Local\\R6ConfigBridge-MVP"+smokeSuffix,out created)){
    if(!created){MessageBox.Show("迁移工具已在运行。");return;}
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    try{using(MainWindow window=new MainWindow()){if(args.Length==2&&args[0]=="--smoke"){window.Smoke(args[1]);return;}Application.Run(window);}}
    catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"last-error.txt"),e.ToString());if(args.Length>0)Environment.ExitCode=1;else MessageBox.Show(e.Message,"启动失败");}
   }
  }
 }
}
