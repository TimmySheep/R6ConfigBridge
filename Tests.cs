using System;
using System.IO;
using System.Text;
using R6ConfigBridge;
class Tests {
 static int count=0;
 static void Check(bool value,string message){if(!value)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
 static void Reject(Action a,string message){bool rejected=false;try{a();}catch{rejected=true;}Check(rejected,message);}
 static byte[] B(string s){return Encoding.UTF8.GetBytes(s);}
 static void Main(){
  string source="; source\r\n[INPUT]\r\nMouseYawSensitivity=5\r\nMousePitchSensitivity=7\r\n[DISPLAY]\r\nFPSLimit=144\r\n";
  string target="; keep comment\r\n[INPUT]\r\nMouseYawSensitivity = 9  \r\nMousePitchSensitivity=10\r\nUnknownFuture=42\r\n[DISPLAY]\r\nFPSLimit=0\r\n";
  Ini a=new Ini(B(source)),b=new Ini(B(target));
  Check(a.Values["INPUT/MouseYawSensitivity"]=="5","parse sensitivity");
  byte[] updated=b.Patch(a,new string[]{"INPUT/MouseYawSensitivity"});
  Check(Encoding.UTF8.GetString(updated)==target.Replace("MouseYawSensitivity = 9  ","MouseYawSensitivity = 5  "),"selective update preserves all other bytes");
  Reject(delegate{b.Patch(a,new string[]{"HARDWARE_INFO/CPUScore"});},"unsafe key rejected");
  Reject(delegate{new Ini(B("[INPUT]\nMouseYawSensitivity=1\nMouseYawSensitivity=2\n"));},"duplicate rejected");
  Reject(delegate{b.Patch(a,new string[]{"INPUT/ADSMouseUseSpecific"});},"missing field rejected");
  byte[] bom=new byte[]{239,187,191}; byte[] content=B(target);byte[] withBom=new byte[content.Length+3];Array.Copy(bom,withBom,3);Array.Copy(content,0,withBom,3,content.Length);
  byte[] patched=new Ini(withBom).Patch(a,new string[]{"INPUT/MouseYawSensitivity"});
  Check(patched[0]==239 && patched[1]==187 && patched[2]==191,"BOM preserved");
  string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-artifacts",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  string src=Path.Combine(root,"source.ini"),dst=Path.Combine(root,"GameSettings.ini");File.WriteAllBytes(src,B(source));File.WriteAllBytes(dst,B(target));
  Snapshot ss=Snapshot.Load(src),ts=Snapshot.Load(dst);
  Reject(delegate{Store.Apply(ss,ts,new string[]{"INPUT/MouseYawSensitivity"},Path.Combine(root,"backups"),delegate{return true;});},"running game blocks writes");
  Check(Store.Hash(File.ReadAllBytes(dst))==ts.Hash,"blocked write leaves target unchanged");
  string backup=Store.Apply(ss,ts,new string[]{"INPUT/MouseYawSensitivity"},Path.Combine(root,"backups"),delegate{return false;});
  Check(new Ini(File.ReadAllBytes(dst)).Values["INPUT/MouseYawSensitivity"]=="5","real sandbox file updated");
  Check(Store.Hash(File.ReadAllBytes(backup))==ts.Hash,"backup byte exact");
  Reject(delegate{Store.Apply(ss,ts,new string[]{"INPUT/MouseYawSensitivity"},Path.Combine(root,"backups"),delegate{return false;});},"stale target refused");
  Store.Restore(backup,dst,Path.Combine(root,"backups"),delegate{return false;});
  Check(Store.Hash(File.ReadAllBytes(dst))==ts.Hash,"restore byte exact");
  File.AppendAllText(src,"; changed\n");
  Reject(delegate{Store.Apply(ss,Snapshot.Load(dst),new string[]{"INPUT/MouseYawSensitivity"},Path.Combine(root,"backups"),delegate{return false;});},"stale source refused");
  Reject(delegate{Store.Restore(backup,src,Path.Combine(root,"backups"),delegate{return false;});},"restore wrong target refused");
  Console.WriteLine("ALL "+count+" CHECKS PASSED; artifacts="+root);
 }
}
