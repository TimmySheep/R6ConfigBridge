using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Web.Script.Serialization;
namespace R6ConfigBridge {
 public class MainWindow:Form {
  readonly ComboBox intl=new ComboBox(),cn=new ComboBox(),direction=new ComboBox(),category=new ComboBox();
  readonly TextBox search=new TextBox();readonly CheckBox different=new CheckBox();
  readonly DataGridView grid=new DataGridView();readonly Label status=new Label(),notice=new Label();
  readonly string backupRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"backups");
  Snapshot left,right;bool loading=false;
  public MainWindow(){
   Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
   Text="R6 配置迁移工具 · MVP 0.1";Width=1240;Height=820;MinimumSize=new Size(1000,650);StartPosition=FormStartPosition.CenterScreen;
   Font=new Font("Microsoft YaHei UI",9);BackColor=Color.FromArgb(246,247,250);
   TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new Padding(18)};
   root.RowStyles.Add(new RowStyle(SizeType.Absolute,45));root.RowStyles.Add(new RowStyle(SizeType.Absolute,80));root.RowStyles.Add(new RowStyle(SizeType.Absolute,60));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));root.RowStyles.Add(new RowStyle(SizeType.Absolute,46));root.RowStyles.Add(new RowStyle(SizeType.Absolute,36));Controls.Add(root);
   root.Controls.Add(new Label{Text="R6 配置迁移  /  国际服 ⇄ 国服",Font=new Font(Font.FontFamily,18,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
   TableLayoutPanel paths=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=2};paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,75));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));paths.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
   paths.Controls.Add(new Label{Text="国际服",AutoSize=true},0,0);paths.Controls.Add(intl,1,0);paths.Controls.Add(Button("手动选择",delegate{Browse(intl);}),2,0);
   paths.Controls.Add(new Label{Text="国服",AutoSize=true},0,1);paths.Controls.Add(cn,1,1);paths.Controls.Add(Button("手动选择",delegate{Browse(cn);}),2,1);
   foreach(ComboBox cb in new[]{intl,cn}){cb.Dock=DockStyle.Fill;cb.DropDownStyle=ComboBoxStyle.DropDownList;cb.SelectedIndexChanged+=delegate{if(!loading)Reload();};}
   root.Controls.Add(paths,0,1);
   notice.Dock=DockStyle.Fill;notice.ForeColor=Color.FromArgb(137,75,0);notice.Text="仅在退出两服后应用。启动器的云同步可能覆盖本地设置，游戏内效果须另行核对。\n具体动作键位（含鼠标侧键绑定）尚未解析，本版不会修改二进制存档。内部硬件、设备 ID、网络字段只读。";root.Controls.Add(notice,0,2);
   FlowLayoutPanel filters=new FlowLayoutPanel{Dock=DockStyle.Fill};
   category.DropDownStyle=ComboBoxStyle.DropDownList;category.Width=175;category.Items.Add("全部类别");foreach(string g in Catalog.Fields.Select(f=>f.Group).Distinct())category.Items.Add(g);category.Items.Add("内部字段（只读）");category.SelectedIndex=0;category.SelectedIndexChanged+=delegate{Filter();};
   search.Width=215;search.TextChanged+=delegate{Filter();};different.Text="只看差异";different.AutoSize=true;different.CheckedChanged+=delegate{Filter();};
   filters.Controls.Add(category);filters.Controls.Add(new Label{Text="搜索",AutoSize=true,Padding=new Padding(0,6,0,0)});filters.Controls.Add(search);filters.Controls.Add(different);filters.Controls.Add(Button("重新检测／刷新",delegate{Discover();}));root.Controls.Add(filters,0,3);
   grid.Dock=DockStyle.Fill;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.AllowUserToResizeRows=false;grid.RowHeadersVisible=false;grid.BackgroundColor=Color.White;grid.BorderStyle=BorderStyle.None;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;
   grid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Pick",HeaderText="选择",FillWeight=40});
   foreach(string h in new[]{"类别","配置项 / 字段名","国际服值","国服值","状态"}){int index=grid.Columns.Add(h,h);grid.Columns[index].ReadOnly=true;}
   grid.Columns[1].FillWeight=95;grid.Columns[2].FillWeight=300;grid.Columns[3].FillWeight=85;grid.Columns[4].FillWeight=85;grid.Columns[5].FillWeight=95;
   grid.CurrentCellDirtyStateChanged+=delegate{if(grid.IsCurrentCellDirty)grid.CommitEdit(DataGridViewDataErrorContexts.Commit);};
   grid.CellValueChanged+=delegate{UpdateStatus();};root.Controls.Add(grid,0,4);
   FlowLayoutPanel select=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0)};
   select.Controls.Add(Button("勾选可见差异",delegate{Choose(true,true);}));select.Controls.Add(Button("勾选可见全部",delegate{Choose(true,false);}));select.Controls.Add(Button("清空勾选",delegate{foreach(DataGridViewRow row in grid.Rows)row.Cells[0].Value=false;}));
   select.Controls.Add(new Label{Text="提示：同步 ADS 时请同时选择开关、乘数和所需倍率；枚举值暂按原始数值显示。",AutoSize=true,Padding=new Padding(6,7,0,0)});root.Controls.Add(select,0,5);
   FlowLayoutPanel actions=new FlowLayoutPanel{Dock=DockStyle.Fill};direction.DropDownStyle=ComboBoxStyle.DropDownList;direction.Items.AddRange(new object[]{"国际服 → 国服","国服 → 国际服"});direction.SelectedIndex=0;direction.Width=170;
   actions.Controls.Add(direction);actions.Controls.Add(Button("预览所选修改",delegate{Preview(false);}));actions.Controls.Add(Button("应用（先备份）",delegate{Preview(true);}));actions.Controls.Add(Button("恢复备份…",delegate{Restore();}));actions.Controls.Add(Button("打开备份目录",delegate{Directory.CreateDirectory(backupRoot);Process.Start("explorer.exe",backupRoot);}));root.Controls.Add(actions,0,6);
   status.Dock=DockStyle.Fill;status.ForeColor=Color.FromArgb(60,70,90);root.Controls.Add(status,0,7);Discover();
  }
  Button Button(string text,Action action){Button b=new Button{Text=text,AutoSize=true,Height=30,FlatStyle=FlatStyle.System};b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"操作未执行／未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};return b;}
  void Browse(ComboBox cb){using(OpenFileDialog d=new OpenFileDialog{Filter="GameSettings.ini|GameSettings.ini",Title="选择对应账号的 GameSettings.ini"})if(d.ShowDialog(this)==DialogResult.OK){if(!string.Equals(Path.GetFileName(d.FileName),"GameSettings.ini",StringComparison.OrdinalIgnoreCase))throw new Exception("只能选择 GameSettings.ini。");if(!cb.Items.Contains(d.FileName))cb.Items.Add(d.FileName);cb.SelectedItem=d.FileName;}}
  void Populate(ComboBox cb,string root){string previous=cb.SelectedItem as string;cb.Items.Clear();if(Directory.Exists(root))foreach(string d in Directory.GetDirectories(root)){string path=Path.Combine(d,"GameSettings.ini");if(File.Exists(path))cb.Items.Add(path);}if(previous!=null&&cb.Items.Contains(previous))cb.SelectedItem=previous;else if(cb.Items.Count==1)cb.SelectedIndex=0;}
  void Discover(){loading=true;try{string docs=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);Populate(intl,Path.Combine(docs,"My Games","Rainbow Six - Siege"));Populate(cn,Path.Combine(docs,"My Games","彩虹六号：攻势"));}finally{loading=false;}Reload();}
  void Reload(){
   grid.Rows.Clear();left=right=null;
   try{
    if(intl.SelectedItem!=null)left=Snapshot.Load((string)intl.SelectedItem);if(cn.SelectedItem!=null)right=Snapshot.Load((string)cn.SelectedItem);
    if(left==null||right==null){status.Text="请选择两服账号配置；多账号不会自动猜选。可使用“手动选择”定位其他安装／文档路径。";return;}
    SortedSet<string> ids=new SortedSet<string>(left.Ini.Values.Keys);ids.UnionWith(right.Ini.Values.Keys);
    foreach(string id in ids){Field f=Catalog.Fields.Find(x=>x.Id==id);string l=left.Ini.Values.ContainsKey(id)?left.Ini.Values[id]:"（缺失）",r=right.Ini.Values.ContainsKey(id)?right.Ini.Values[id]:"（缺失）";bool safe=f!=null&&left.Ini.Values.ContainsKey(id)&&right.Ini.Values.ContainsKey(id);
     int n=grid.Rows.Add(false,f!=null?f.Group:"内部字段（只读）",f!=null?f.Label+"  ["+id+"]":id,l,r,!safe?"只读 / 不支持":l==r?"相同":"不同");DataGridViewRow row=grid.Rows[n];row.Tag=id;row.Cells[0].ReadOnly=!safe;
     if(!safe){row.DefaultCellStyle.ForeColor=Color.Gray;row.Cells[0].Style.BackColor=Color.FromArgb(235,235,235);}else if(l!=r)row.DefaultCellStyle.BackColor=Color.FromArgb(255,248,225);
    }Filter();
   }catch(Exception e){status.Text="读取失败："+e.Message;}
  }
  void Filter(){grid.CurrentCell=null;foreach(DataGridViewRow row in grid.Rows){string g=Convert.ToString(row.Cells[1].Value),s=Convert.ToString(row.Cells[2].Value);row.Visible=(category.SelectedIndex==0||g==Convert.ToString(category.SelectedItem))&&(!different.Checked||Convert.ToString(row.Cells[3].Value)!=Convert.ToString(row.Cells[4].Value))&&(search.Text.Length==0||s.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase)>=0);}UpdateStatus();}
  void Choose(bool pick,bool onlyDiff){foreach(DataGridViewRow row in grid.Rows)if(row.Visible&&!row.Cells[0].ReadOnly&&(!onlyDiff||Convert.ToString(row.Cells[3].Value)!=Convert.ToString(row.Cells[4].Value)))row.Cells[0].Value=pick;}
  string[] Picks(){grid.EndEdit();return grid.Rows.Cast<DataGridViewRow>().Where(r=>Convert.ToBoolean(r.Cells[0].Value)&&!r.Cells[0].ReadOnly).Select(r=>(string)r.Tag).ToArray();}
  void UpdateStatus(){int selected=grid.Rows.Cast<DataGridViewRow>().Count(r=>Convert.ToBoolean(r.Cells[0].Value));int available=grid.Rows.Cast<DataGridViewRow>().Count(r=>!r.Cells[0].ReadOnly);status.Text="已加载 "+grid.Rows.Count+" 项 / 可选择 "+available+" 项 / 已勾选 "+selected+" 项（包括被筛选隐藏的勾选项）。默认不自动修改任何配置。";}
  void Preview(bool apply){
   if(left==null||right==null)throw new Exception("请先选择两份配置。");Snapshot src=direction.SelectedIndex==0?left:right,dst=direction.SelectedIndex==0?right:left;string[] ids=Picks();dst.Ini.Patch(src.Ini,ids);
   List<string> changed=ids.Where(id=>src.Ini.Values[id]!=dst.Ini.Values[id]).ToList();if(changed.Count==0)throw new Exception("所选项目没有差异。");
   StringBuilder text=new StringBuilder();text.AppendLine(Convert.ToString(direction.SelectedItem));text.AppendLine("来源："+src.Path);text.AppendLine("目标："+dst.Path);text.AppendLine();foreach(string id in changed)text.AppendLine(id+" : "+dst.Ini.Values[id]+" → "+src.Ini.Values[id]);
   text.AppendLine();text.AppendLine("只修改以上 "+changed.Count+" 个差异字段，其他字段、注释和换行保留。具体动作键位不包含在内。云同步和游戏内生效需另行验证。");
   using(Form dialog=new Form{Text=apply?"确认迁移（目标将先备份）":"修改预览（不会写入）",Width=820,Height=560,StartPosition=FormStartPosition.CenterParent,Font=Font}){
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
   Directory.CreateDirectory(dir);Show();Application.DoEvents();int n=grid.Rows.Count,available=grid.Rows.Cast<DataGridViewRow>().Count(r=>!r.Cells[0].ReadOnly);if(n==0||available==0)throw new Exception("No real configurations loaded");
   Choose(true,true);int selected=Picks().Length;if(selected==0)throw new Exception("No differences selected");different.Checked=true;search.Text="Mouse";Application.DoEvents();int filtered=grid.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible);search.Clear();different.Checked=false;
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
   object result=new{Rows=n,Available=available,SelectedDifferences=selected,MouseFilteredRows=filtered,ReversePreviewBytes=preview.Length,GameRunning=Store.GameRunning(),RealConfigsUntouched=true,Sandbox=sandbox};
   string json=new JavaScriptSerializer().Serialize(result);File.WriteAllText(Path.Combine(dir,"smoke-result.json"),json,Encoding.UTF8);Close();return result;
  }
 }
 static class Program {
  [STAThread]static void Main(string[] args){
   bool created;using(Mutex mutex=new Mutex(true,"Local\\R6ConfigBridge-MVP",out created)){
    if(!created){MessageBox.Show("迁移工具已在运行。");return;}
    Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    try{using(MainWindow window=new MainWindow()){if(args.Length==2&&args[0]=="--smoke"){window.Smoke(args[1]);return;}Application.Run(window);}}
    catch(Exception e){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"last-error.txt"),e.ToString());if(args.Length>0)Environment.ExitCode=1;else MessageBox.Show(e.Message,"启动失败");}
   }
  }
 }
}
