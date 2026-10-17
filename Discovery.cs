using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace R6ConfigBridge {
 public class ScanSide {
  public string[] Configs=new string[0],Evidence=new string[0],Errors=new string[0];
  public override string ToString(){return (Evidence.Length==0?"未发现安装线索（安装状态未确定）":string.Join("；",Evidence.Distinct().ToArray()))+"\n"+(Configs.Length==0?"未发现配置（可能尚未启动游戏）":"发现配置 "+Configs.Length+" 份")+(Errors.Length==0?"":"；扫描错误 "+Errors.Length+" 项（结果可能不完整）");}
 }
 public class ScanResult {public ScanSide International=new ScanSide(),China=new ScanSide();}
 // Only known shallow locations and platform records; no recursive disk scan.
 public static class Discovery {
  static void Configs(ScanSide side,string root){
   List<string> paths=new List<string>();List<string> errors=side.Errors.ToList();
   try{if(Directory.Exists(root)){string[] dirs=Directory.GetDirectories(root);if(dirs.Length>256)errors.Add("账号目录超过 256 个，已截断");foreach(string d in dirs.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).Take(256)){try{string f=Path.Combine(d,"GameSettings.ini");if(File.Exists(f)){using(FileStream stream=File.Open(f,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){}paths.Add(f);}}catch(Exception ex){errors.Add("配置读取："+ex.Message);}}}}
   catch(Exception ex){errors.Add("配置扫描："+ex.Message);}side.Configs=paths.ToArray();side.Errors=errors.ToArray();
  }
  public static ScanResult Inspect(string documents,string[] international,string[] china,string[] errors){
   ScanResult result=new ScanResult();result.International.Evidence=international;result.China.Evidence=china;result.International.Errors=(string[])errors.Clone();result.China.Errors=(string[])errors.Clone();
   Configs(result.International,Path.Combine(documents,"My Games","Rainbow Six - Siege"));Configs(result.China,Path.Combine(documents,"My Games","彩虹六号：攻势"));return result;
  }
  static string Value(RegistryKey key,string name){return Convert.ToString(key.GetValue(name));}
  static string Evidence(string platform,string location){
   bool game=false;if(!string.IsNullOrWhiteSpace(location)){string root=Environment.ExpandEnvironmentVariables(location.Trim('"'));game=File.Exists(Path.Combine(root,"RainbowSix.exe"))||File.Exists(Path.Combine(root,"RainbowSix_Vulkan.exe"))||File.Exists(Path.Combine(root,"RainbowSix_DX11.exe"));}
   return game?"已检测到安装（"+platform+"记录及游戏文件）":"候选："+platform+"记录（未核实游戏文件）";
  }
  // DisplayName identifies the edition; a directory name alone never does.
  public static int Edition(string name){
   if(string.IsNullOrEmpty(name))return 0;string n=name.ToLowerInvariant();
   if(n.Contains("彩虹六号：攻势")||n.Contains("彩虹六号:攻势")||n.Contains("彩虹六号攻势")||n.Contains("rainbow six cn"))return 2;
   if(n.Contains("rainbow six")&&(n.Contains("siege")||n.Contains("围攻")))return 1;return 0;
  }
  static void Uninstall(RegistryKey hive,List<string> intl,List<string> cn,List<string> errors){
   using(RegistryKey uninstall=hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))if(uninstall!=null){string[] ids=uninstall.GetSubKeyNames();if(ids.Length>4096)errors.Add("卸载记录超过扫描上限");foreach(string id in ids.Take(4096)){try{using(RegistryKey app=uninstall.OpenSubKey(id)){if(app==null)continue;int side=Edition(Value(app,"DisplayName"));if(side!=0)(side==1?intl:cn).Add(Evidence("卸载",Value(app,"InstallLocation")));}}catch(Exception e){errors.Add("卸载记录："+e.Message);}}}
  }
  static IEnumerable<string> Libraries(string root){
   List<string> roots=new List<string>{root};string file=Path.Combine(root,"steamapps","libraryfolders.vdf");
   if(File.Exists(file)){FileInfo info=new FileInfo(file);if(info.Length>1024*1024)throw new Exception("Steam 库记录过大");foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s*\"([^\"]+)\"")){roots.Add(m.Groups[1].Value.Replace("\\\\","\\"));}}
   return roots.Distinct(StringComparer.OrdinalIgnoreCase).Take(32);
  }
  static void Steam(string root,List<string> intl,List<string> errors){
   if(string.IsNullOrWhiteSpace(root))return;try{foreach(string library in Libraries(root)){
    string manifest=Path.Combine(library,"steamapps","appmanifest_359550.acf");if(!File.Exists(manifest))continue;
    if(new FileInfo(manifest).Length>1024*1024)throw new Exception("Steam 游戏清单过大");string text=File.ReadAllText(manifest);
    if(!Regex.IsMatch(text,"\"appid\"\\s*\"359550\""))continue;Match dir=Regex.Match(text,"\"installdir\"\\s*\"([^\"]+)\"");
    string location=dir.Success?Path.Combine(library,"steamapps","common",dir.Groups[1].Value):"";intl.Add(Evidence("Steam 359550",location));
   }}catch(Exception e){errors.Add("Steam："+e.Message);}
  }
  public static ScanResult Scan(){
   List<string> intl=new List<string>(),cn=new List<string>(),errors=new List<string>(),steam=new List<string>();
   foreach(RegistryView view in new[]{RegistryView.Registry64,RegistryView.Registry32})foreach(RegistryHive kind in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser}){
    try{using(RegistryKey hive=RegistryKey.OpenBaseKey(kind,view)){
     Uninstall(hive,intl,cn,errors);
     using(RegistryKey install=hive.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs\1843"))if(install!=null)intl.Add(Evidence("Ubisoft 1843",Value(install,"InstallDir")));
     using(RegistryKey key=hive.OpenSubKey(@"SOFTWARE\Valve\Steam"))if(key!=null){string path=Value(key,"SteamPath");if(path.Length==0)path=Value(key,"InstallPath");if(path.Length>0)steam.Add(path);}
    }}catch(Exception e){errors.Add("注册表："+e.Message);}
   }
   foreach(string root in steam.Distinct(StringComparer.OrdinalIgnoreCase).Take(8))Steam(root,intl,errors);
   return Inspect(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),intl.Distinct().ToArray(),cn.Distinct().ToArray(),errors.ToArray());
  }
 }
}
