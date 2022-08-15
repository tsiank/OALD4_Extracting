//注册big5编码
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

//读取JRDCONV.DAT
var jrdconvn = @"D:\WinXpVirt\数据文件\JRDCONV.DAT";
var covnBuffer = File.ReadAllBytes(jrdconvn);

//获取JRDOVFL词条位置
int startPoint = 842; //从此处开始读取
var conlist = new List<int>();
for (var i = 1; i <= 22294; i++)
{
	var serialHex = i.ToString("X4");
	while (startPoint < covnBuffer.Length)
	{
		if (covnBuffer[startPoint -1] == 0x00 && covnBuffer[startPoint -2] == 0x00 && covnBuffer[startPoint].ToString("X2") + covnBuffer[startPoint + 1].ToString("X2") == serialHex && 
			covnBuffer[startPoint + 2] == 0x00 && covnBuffer[startPoint + 3] == 0x00)
		{
			conlist.Add(startPoint);
			break;
		}

		startPoint += 1;
	}
}
conlist.Add(covnBuffer.Length);

//根据词条位置分割词条
List<OALDDef> convDefList = new();

var total = conlist.Count();

Regex r = new Regex("FF00[0-F]{6}"); //删除FF00后面三个字节
for (var s = 1; s < total; s++)
{	
	var sb = new StringBuilder();
	for (var k = conlist[s - 1]; k < conlist[s]; k++)
	{
		sb.Append(covnBuffer[k].ToString("X2"));
	}
	
	var def = sb.ToString();
	var serialHex = def[..4];
	var	content = def[12..];

	//处理同一词条多个FF00的情况
	var arr = r.Replace(content, "FF00 ").Split(" ");

	if (arr.Length > 2)
	{
		for (var f = 0; f < arr.Length - 1; f++)
		{
			if (f < arr.Length - 1)
			{
				convDefList.Add(
					new OALDDef()
					{
						SerialTag = $"{serialHex}_{f}",
						SerialHex = serialHex,
						Def = arr[f]
					});
			}
			else
			{
				convDefList.Add(
					new OALDDef()
					{
						SerialTag = $"{serialHex}_{f}",
						SerialHex = serialHex,
						Def = arr[f] + arr[f + 1]
					});
			}

		}

	}
	else
	{
		convDefList.Add(
			new OALDDef()
			{
				SerialTag = serialHex,
				SerialHex = serialHex,
				Def = r.Replace(content, "FF00")
			});
	}

}


//读取JRDOVFL
var jrdovfl = @"D:\WinXpVirt\数据文件\JRDOVFL.DAT";
var ovflBuffer = File.ReadAllBytes(jrdovfl);

List<int> ovList = new List<int>();
ovList.Add(2);  //JRDOVFL.DAT从第2字节开始读取

//获取JRDOVFL词条位置
List<OALDDef> ovDefList = new();
startPoint = 2;
while (startPoint <= ovflBuffer.Length - 4)
{
	//根据是连续4个00还是3个00判断两词条的分界
	if (ovflBuffer[startPoint] == 0x00 && ovflBuffer[startPoint + 1] == 0x00 && ovflBuffer[startPoint + 2] == 0x00 && ovflBuffer[startPoint + 3] == 0x00)
	{
		startPoint += 3;
		ovList.Add(startPoint);
	}
	else if (ovflBuffer[startPoint] == 0x00 && ovflBuffer[startPoint + 1] == 0x00 && ovflBuffer[startPoint + 2] == 0x00)
	{
		startPoint += 3;
		ovList.Add(startPoint);
	}
	else
	{
		startPoint += 1;
	}

}

//根据词条位置分割词条
total = ovList.Count();
for (var s = 1; s < total; s++)
{
	var sb = new StringBuilder();
	for (var k = ovList[s - 1]; k < ovList[s]; k++)
	{
		sb.Append(ovflBuffer[k].ToString("X2"));
	}
	
	var def = sb.ToString();
	ovDefList.Add(
		new OALDDef()
		{
			SerialTag = def[..4],
			SerialHex = def[..4],
			Def = def[4..]
		});
}

//给JRDOVFL.DAT中相同的词条编号
var dic = new Dictionary<string, int>();
var dic2 = new Dictionary<string, int>();
for (var d = 0; d < ovDefList.Count(); d++)
{
	if (!dic.ContainsKey(ovDefList[d].SerialTag))
	{
		dic.Add(ovDefList[d].SerialTag, 0);
		dic2.Add(ovDefList[d].SerialTag, d);
	}
	else
	{
		int originPos = dic2[ovDefList[d].SerialTag];
		ovDefList[originPos].SerialTag = ovDefList[d].SerialTag + "_" + 0;

		dic[ovDefList[d].SerialTag] += 1;
		ovDefList[d].SerialTag = ovDefList[d].SerialTag + "_" + dic[ovDefList[d].SerialTag];

	}
}

//拼接两文件中的同一词条
var defsCombine = from con in convDefList
				  join ov in ovDefList on con.SerialTag equals ov.SerialTag into conov
				  from cv in conov.DefaultIfEmpty()
				  //select $"{Convert.ToInt32(con.SerialHex, 16)}\t{HandleHex(con.Def.Replace("FF00", (cv?.Def == null ? "" : cv.Def)))}";
				  select new OALDDef
				  {
				  	SerialHex = Convert.ToInt32(con.SerialHex, 16).ToString(),
					Def = HandleHex(con.Def.Replace("FF00", (cv?.Def == null ? "" : cv.Def)))
				  };

//合并同一序号
var defsLast = defsCombine.GroupBy(c => c.SerialHex).Select(d => $"{d.Key}\t{string.Join("\t", d.Select(e => e.Def))}");

//保存JRDCONV
var defa = from con in convDefList
		   select $"{Convert.ToInt32(con.SerialHex, 16)}\t{con.SerialTag}\t{HandleHex(con.Def)}";
string convFile = @"D:\WinXpVirt\数据文件\OALD4_Conv.txt";
WriteListToTextFile(defa.ToList(), convFile);

//保存JRDOVFL
var defb = from ov in ovDefList
		   select $"{Convert.ToInt32(ov.SerialHex, 16)}\t{ov.SerialTag}\t{HandleHex(ov.Def)}";
string ovFile = @"D:\WinXpVirt\数据文件\OALD4_Ovfl.txt";
WriteListToTextFile(defb.ToList(), ovFile);


//保存最终结果
string saveDefFile = @"D:\WinXpVirt\数据文件\OALD4-Complate.txt";
WriteListToTextFile(defsLast.ToList(), saveDefFile);


string HandleHex(string s)
{
	string C(string s)
	{
		return Encoding.GetEncoding("Big5").GetString(HexStringToByteArray(s));
	}

	//替换xml标签
	var sb = new StringBuilder(C(s));
	sb.Replace(C("FF50"), "<sup>").Replace(C("FF70"), "</sup>"); //词条上标标签
	sb.Replace(C("FF58"), "<pho>").Replace(C("FF78"), "</pho>"); //音标标签
	sb.Replace(C("FF42"), "<b>").Replace(C("FF62"), "</b>"); //粗体标签
	sb.Replace(C("FF49"), "<i>").Replace(C("FF69"), "</i>"); //斜体标签
	sb.Replace(C("FF57"), "<cn>").Replace(C("FF77"), "</cn>"); //中文标签
	sb.Replace(C("FF0F"), @"<a href=""entry://").Replace(C("FF10"), @""">").Replace(C("FF11"), "</a>"); //超链接标签
	var ret = sb.ToString().TrimEnd();
	return ret;
}

//16进制字符串转字节数组
byte[] HexStringToByteArray(string s)
{
	byte[] buffer = new byte[s.Length / 2];
	for (int i = 0; i < s.Length; i += 2)
		buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
	return buffer;
}

//将List保存为TXT文件
void WriteListToTextFile(List<string> list, string txtFile)
{
	using FileStream fs = new FileStream(txtFile, FileMode.OpenOrCreate, FileAccess.Write);
	using StreamWriter sw = new StreamWriter(fs, System.Text.Encoding.UTF8);
	sw.Flush();
	sw.BaseStream.Seek(0, SeekOrigin.Begin);
	for (int i = 0; i < list.Count; i++)
	{
		sw.WriteLine(list[i]);
	}
	sw.Flush();
}


class OALDDef
{
	public string SerialTag { get; set; }
	public string? SerialHex { get; set; }  //词条编号
	public string? Def { get; set; } //词条内容
}


