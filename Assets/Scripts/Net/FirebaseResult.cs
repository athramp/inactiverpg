using System;
using System.Collections;
using System.Collections.Generic;

public static class FirebaseResult {
  public static IDictionary<string, object> ToDict(object data) {
    if (data == null) return new Dictionary<string, object>();
    if (data is IDictionary<string, object> d1) return d1;
    if (data is IDictionary d2) {
      var outd = new Dictionary<string, object>();
      foreach (DictionaryEntry e in d2) outd[Convert.ToString(e.Key)] = e.Value;
      return outd;
    }
    return new Dictionary<string, object>();
  }

  public static long GetLong(IDictionary<string, object> d, string k, long def=0) {
    if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
    if (v is long l) return l;
    if (v is int i) return i;
    if (v is double dbl) return (long)Math.Round(dbl);
    long.TryParse(Convert.ToString(v), out def); return def;
  }

  public static string GetString(IDictionary<string, object> d, string k, string def="") =>
    d != null && d.TryGetValue(k, out var v) ? Convert.ToString(v) : def;

  public static bool GetBool(IDictionary<string, object> d, string k, bool def=false) {
    if (d == null || !d.TryGetValue(k, out var v) || v == null) return def;
    if (v is bool b) return b;
    bool.TryParse(Convert.ToString(v), out def); return def;
  }
}
