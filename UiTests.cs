using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using R6ConfigBridge;
class UiTests {
 static int count;
 static object Get(object o,string n){return o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static object Call(object o,string n,params object[] a){return o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,a);}
 static void Check(bool b,string s){if(!b)throw new Exception(s);count++;Console.WriteLine("PASS "+s);}
 [STAThread] static void Main(){try{Run();}catch(Exception e){Console.WriteLine("FAIL "+e.Message);Environment.ExitCode=1;}}
 static void Run(){Application.EnableVisualStyles();using(MainWindow w=new MainWindow()){
 w.Show();Application.DoEvents();DataGridView g=(DataGridView)Get(w,"grid");
 Check(g.Columns[4].DisplayIndex<g.Columns[3].DisplayIndex,"CN values left of international");
 Check(typeof(MainWindow).GetField("details",BindingFlags.Instance|BindingFlags.NonPublic)!=null,"technical details toggle exists");
 Check(!((CheckBox)Get(w,"details")).Checked,"details default off");
 string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-artifacts","ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
 string a=Path.Combine(root,"intl-raw-id.ini"),b=Path.Combine(root,"cn-raw-id.ini");
 string ini="";foreach(var section in Catalog.Fields.GroupBy(f=>f.Id.Split('/')[0])){ini+="["+section.Key+"]\n";foreach(Field f in section)ini+=f.Id.Split('/')[1]+"=1\n";}ini+="[INTERNAL]\nSecretDevice=abc\n";
 File.WriteAllText(a,ini);File.WriteAllText(b,ini.Replace("=1","=2"));
 typeof(MainWindow).GetField("loading",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,true);
 ComboBox intl=(ComboBox)Get(w,"intl"),cn=(ComboBox)Get(w,"cn");intl.Items.Add(a);intl.SelectedItem=a;cn.Items.Add(b);cn.SelectedItem=b;
 typeof(MainWindow).GetField("loading",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(w,false);Call(w,"Reload");
 Check(!intl.GetItemText(a).Contains("raw-id")&&!intl.GetItemText(a).Contains(root),"account labels hide raw paths and IDs");
 Check(g.Rows.Cast<DataGridViewRow>().Where(r=>r.Visible).Count()==Catalog.Fields.Count,"only supported rows shown by default");
 foreach(DataGridViewRow r in g.Rows)if(r.Visible){string name=Convert.ToString(r.Cells[2].Value),id=(string)r.Tag;Check(!name.Contains(id.Split('/')[1])&&System.Text.RegularExpressions.Regex.IsMatch(name,"[\\u4e00-\\u9fff]"),"friendly label "+id);}
 Call(w,"Choose",true,true);string[] picks=(string[])Call(w,"Picks");Check(picks.Length==Catalog.Fields.Count,"all supported differences selectable");
 ((CheckBox)Get(w,"details")).Checked=true;Check(g.Rows.Cast<DataGridViewRow>().Any(r=>r.Visible&&(string)r.Tag=="INTERNAL/SecretDevice"),"technical mode reveals unknown rows");
 Check(intl.GetItemText(a)==a,"technical mode reveals full path");
 ((CheckBox)Get(w,"details")).Checked=false;Check(picks.SequenceEqual((string[])Call(w,"Picks")),"detail toggling preserves picks");
 Check(cn.Parent.Parent is GroupBox&&intl.Parent.Parent is GroupBox,"two separate profile groups");
 Check(cn.Parent.Parent.Left<intl.Parent.Parent.Left&&Math.Abs(cn.Parent.Parent.Width-intl.Parent.Parent.Width)<=1,"CN profile left equal width");
 Check(typeof(MainWindow).GetMethod("PreviewText",BindingFlags.Instance|BindingFlags.NonPublic)!=null,"testable preview text exists");
 string forward=(string)Call(w,"PreviewText");Check(forward.Contains("2 → 1")&&forward.Contains("国际服 → 国服")&&!forward.Contains(root)&&!forward.Contains("INPUT/"),"forward preview friendly correct target/source");
 ((ComboBox)Get(w,"direction")).SelectedIndex=1;string reverse=(string)Call(w,"PreviewText");Check(reverse.Contains("1 → 2")&&reverse.Contains("国服 → 国际服"),"reverse preview correct target/source");
 ((CheckBox)Get(w,"details")).Checked=true;Check(((string)Call(w,"PreviewText")).Contains(a)&&((string)Call(w,"PreviewText")).Contains("INPUT/"),"technical preview includes identifiers");
 cn.SelectedIndex=-1;Call(w,"Reload");Check(g.Rows.Count>0&&g.Rows.Cast<DataGridViewRow>().All(r=>Convert.ToString(r.Cells[4].Value)=="（未加载）"),"single-side values visible missing CN explicit");
 Check(g.Rows.Cast<DataGridViewRow>().All(r=>r.Cells[0].ReadOnly),"single-side migration selections disabled");
 bool refused=false;try{Call(w,"PreviewText");}catch(TargetInvocationException e){refused=e.InnerException.Message.Contains("两份");}Check(refused,"single-side preview and apply guard refuses");
 cn.SelectedItem=b;intl.SelectedIndex=-1;Call(w,"Reload");Check(g.Rows.Count>0&&g.Rows.Cast<DataGridViewRow>().All(r=>Convert.ToString(r.Cells[3].Value)=="（未加载）"),"CN-only values visible missing international explicit");
 Check(typeof(MainWindow).Assembly.GetType("R6ConfigBridge.Discovery")!=null,"bounded discovery service exists");
 Check(Descendants(w).OfType<Button>().Any(x=>x.Text=="扫描已安装游戏"),"explicit installed-game scan button");
 Type discovery=typeof(MainWindow).Assembly.GetType("R6ConfigBridge.Discovery");
 Func<string,string[],string[],string[],object> inspect=(docs,ie,ce,errors)=>discovery.GetMethod("Inspect").Invoke(null,new object[]{docs,ie,ce,errors});
 string docsFixture=Path.Combine(root,"Documents");string intlFixture=Path.Combine(docsFixture,"My Games","Rainbow Six - Siege","fixture-account");Directory.CreateDirectory(intlFixture);File.WriteAllText(Path.Combine(intlFixture,"GameSettings.ini"),ini);
 object result=inspect(docsFixture,new string[0],new string[0],new string[0]);object international=Public(result,"International"),china=Public(result,"China");
 Check(((string[])Public(international,"Configs")).Length==1&&((string[])Public(china,"Configs")).Length==0,"fixture scan discovers one-side config");
 Check(china.ToString().Contains("未发现")&&!china.ToString().Contains("未安装"),"missing config never means not installed");
 result=inspect(docsFixture,new[]{"已检测到安装（夹具平台记录及文件）"},new[]{"候选：夹具卸载记录"},new string[0]);
 Check(Public(result,"International").ToString().Contains("已检测到安装")&&Public(result,"China").ToString().Contains("候选")&&Public(result,"China").ToString().Contains("未发现配置"),"install evidence distinguished from config and candidate");
 result=inspect(docsFixture,new string[0],new string[0],new[]{"fixture access denied"});Check(Public(result,"China").ToString().Contains("扫描错误"),"scan errors not reported as not found only");
 cn.SelectedIndex=-1;intl.SelectedIndex=-1;Call(w,"UseScan",inspect(docsFixture,new string[0],new string[0],new string[0]));Check(intl.Items.Contains(Path.Combine(intlFixture,"GameSettings.ini")),"scan result loads available config choices");
 Check(!Descendants(w).OfType<Button>().First(x=>x.Text=="应用（先备份）").Enabled,"apply button disabled with only one side");
 Check((int)discovery.GetMethod("Edition").Invoke(null,new object[]{"Rainbow Six Siege"})==1&&(int)discovery.GetMethod("Edition").Invoke(null,new object[]{"彩虹六号：攻势"})==2&&(int)discovery.GetMethod("Edition").Invoke(null,new object[]{"C:\\Games\\Siege"})==0,"edition requires recognized platform title not folder name");
 Button scanButton=Descendants(w).OfType<Button>().First(x=>x.Text=="扫描已安装游戏");scanButton.PerformClick();Check(!scanButton.Enabled,"scan runs asynchronously with reentry disabled");DateTime until=DateTime.UtcNow.AddSeconds(30);while(!scanButton.Enabled&&DateTime.UtcNow<until){Application.DoEvents();System.Threading.Thread.Sleep(10);}Check(scanButton.Enabled&&((Label)Get(w,"cnScan")).Text.Contains("配置")&&((Label)Get(w,"intlScan")).Text.Contains("配置"),"real bounded scan completes separate side statuses");
 Console.WriteLine("ALL "+count+" UI CHECKS PASSED");}}
 static object Public(object o,string n){return o.GetType().GetField(n).GetValue(o);}
 static System.Collections.Generic.IEnumerable<Control> Descendants(Control p){foreach(Control c in p.Controls){yield return c;foreach(Control child in Descendants(c))yield return child;}}
}
