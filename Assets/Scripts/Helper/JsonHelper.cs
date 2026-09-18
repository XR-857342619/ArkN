using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

public static class JsonHelper
{
	static JsonSerializerSettings setting;
	static JsonSerializerSettings typeSerializerSetting;
	static JsonHelper()
	{
		// 注意：Newtonsoft.Json-for-Unity.Converters 会把 JsonConvert.DefaultSettings 的
		// DefaultValueHandling 设为 IgnoreAndPopulate，导致：
		//   - 反序列化时，JSON 中缺失的字段会被填成 default(T)（float 即 0），覆盖字段初始值；
		//   - 序列化时，等于 default(T) 的字段不会被写入，0 值无法持久化。
		// 这会破坏 GameData.Bgm = 1 / BgmVolume = 1 这类初始值，因此这里显式覆盖为 Include，
		// 让字段初始值生效、并让 0 值也能正常存取。
		setting = new JsonSerializerSettings()
		{
			DefaultValueHandling = DefaultValueHandling.Include,
		};
		typeSerializerSetting = new JsonSerializerSettings()
		{
			TypeNameHandling = TypeNameHandling.Auto,
			DefaultValueHandling = DefaultValueHandling.Include,
		};
		//JsonSerializerSettings setting = new Newtonsoft.Json.JsonSerializerSettings();
		//JsonConvert.DefaultSettings = new Func<JsonSerializerSettings>(() =>
		//{
		//	//日期类型默认格式化处理
		//	//setting.DateFormatHandling = Newtonsoft.Json.DateFormatHandling.MicrosoftDateFormat;
		//	//setting.DateFormatString = "yyyy-MM-dd HH:mm:ss";

		//	//空值处理
		//	setting.NullValueHandling = NullValueHandling.Ignore;
		//	setting.DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate;
		//	setting.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
		//	//高级用法九中的Bool类型转换 设置
		//	//setting.Converters.Add(new BoolConvert("是,否"));

		//	return setting;
		//});
	}
	public static void Init()
	{
		// 调用这个是为了调用静态方法
	}

	public static string ToJson(object obj)
	{
		return JsonConvert.SerializeObject(obj, setting);
	}
	public static T FromJson<T>(string str)
	{
		return JsonConvert.DeserializeObject<T>(str, setting);
	}

	public static string ToJsonWithType(object obj)
	{
		return JsonConvert.SerializeObject(obj,typeSerializerSetting);
	}
	public static T FromJsonWithType<T>(string str)
	{
		return JsonConvert.DeserializeObject<T>(str, typeSerializerSetting);
	}

	public static object FromJson(Type type, string str)
	{
		return JsonConvert.DeserializeObject(str, type, setting);
	}

	public static T Clone<T>(T t)
	{
		return FromJson<T>(ToJson(t));
	}
}