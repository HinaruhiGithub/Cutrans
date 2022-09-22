using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.IO;
using System.Text;
using System.Xml;

using System.Collections.Specialized;
using static TexTra.APIAccessor_ja.HttpConnection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

/// <summary>
/// APIアクセス部
/// ソース公開のためのもの
/// どこにも使用されていない
/// </summary>
namespace TexTra
{

    public static class APIAccessor_ja
    {

        #region "API"

        #region "対訳登録"

        /// <summary>
        /// 対訳を登録
        /// </summary>
        /// <param name="bean"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static APIResponseBean regist_taiyaku(RegistTaiyaku bean)
        {
            try
            {
                return APIAccessor_ja.get_data_from_server(bean);
            }
            catch (Exception e)
            {
                return new APIResponseBean(e);
            }
        }

        /// <summary>
        /// 対訳登録
        /// </summary>
        /// <remarks></remarks>
        public class RegistTaiyaku : ServerInfoBean
        {

            public RegistTaiyaku()
            {
                this.set_api_url("register/bilingual");
            }

            public string pid { get; set; }
            public string text_s { get; set; }
            public string text_t { get; set; }

        }

        #endregion

        #region "API 文章区切り"

        /// <summary>
        /// 文章区切り
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        static internal APIResponseBean split_sentence(string text, Language lang)
        {

            // 空文字
            if (string.IsNullOrWhiteSpace(text))
            {
                APIResponseBean resp_err = new APIResponseBean();
                resp_err.error_message = "文章区切りの対象のテキストがありません。";
                return resp_err;
            }

            // textが長すぎる場合は区切る
            List<string> list_text = new List<string>();
            List<string> list_split = new List<string>(text.Split("\r".ToCharArray()));
            StringBuilder str_temp = new StringBuilder();
            foreach (String txt_split in list_split)
            {
                str_temp.Append(txt_split + "\r");
                if (str_temp.Length > 3000) { list_text.Add(str_temp.ToString()); str_temp.Clear(); }
            }
            if (str_temp.Length != 0) { str_temp.Remove(str_temp.Length - 1, 1); list_text.Add(str_temp.ToString()); }

            // API「文章区切り」にアクセス
            try
            {
                List<string> list_result = new List<string>();
                APIResponseBean resp_bean = null;

                foreach (String txt in list_text)
                {
                    SplitSentenceInfo search_bean = new SplitSentenceInfo();
                    search_bean.text = txt;
                    search_bean.language = lang;

                    resp_bean = APIAccessor_ja.get_data_from_server(search_bean);
                    if (resp_bean.is_error) return resp_bean;

                    SplitSentenceResultInfo bean_split = SplitSentenceInfo.convert_from_server_response(resp_bean);
                    resp_bean.value = bean_split;
                    list_result.AddRange(bean_split.list_text);
                }

                SplitSentenceResultInfo bean_split_all = (SplitSentenceResultInfo)resp_bean.value;
                bean_split_all.list_text = list_result;
                return resp_bean;

            }
            catch (Exception e)
            {
                return new APIResponseBean(e);
            }

        }

        /// <summary>
        /// 文章区切り Request Bean
        /// </summary>
        /// <remarks></remarks>
        public class SplitSentenceInfo : ServerInfoBean
        {

            public SplitSentenceInfo()
            {
                this.set_api_url("split");
            }

            public Language language = Language.none;
            public string lang
            {
                get { return get_lang_cd(this.language); }
            }

            public string text { get; set; }

            /// <summary>
            /// サーバレスポンスを変換
            /// </summary>
            /// <param name="resp_bean"></param>
            /// <returns></returns>
            /// <remarks></remarks>
            static internal SplitSentenceResultInfo convert_from_server_response(APIResponseBean resp_bean)
            {

                SplitSentenceResultInfo bean = new SplitSentenceResultInfo();

                List<string> list = new List<string>();
                bean.list_text = list;

                foreach (XmlElement node in resp_bean.result.SelectNodes("/result/text/item"))
                {
                    list.Add(node.InnerText);
                }

                return bean;

            }

        }

        /// <summary>
        /// 文章区切り Response Bean
        /// </summary>
        /// <remarks></remarks>
        public class SplitSentenceResultInfo
        {
            public List<string> list_text { get; set; }
        }

        #endregion

        #region "API 自動翻訳"

        /// <summary>
        /// 自動翻訳
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        static public List<APIResponseBean> get_auto_trans(string text, Language lang_org, Language lang_trans,
                                                            out APIResponseBean err_resp_bean)
        {
            err_resp_bean = null;

            //複数ある翻訳サーバの情報を取得
            //翻訳サーバ情報（URLなど）は
            //ユーザによって手動設定されたものを使用
            List<GetAutoTransInfo> list_search_bean = GetAutoTransInfo.get_all_auto_trans_info(text, lang_org, lang_trans);

            //サーバごとに翻訳結果を取得
            List<APIResponseBean> list_resp_bean = new List<APIResponseBean>();
            foreach (GetAutoTransInfo search_bean in list_search_bean)
            {
                APIResponseBean resp_bean = APIAccessor_ja.get_auto_trans(search_bean);
                if (resp_bean.is_error) { err_resp_bean = resp_bean; continue; }
                AutoTransInfo trans_info = (AutoTransInfo)resp_bean.value;
                trans_info.server_name = search_bean.api_name;
                list_resp_bean.Add(resp_bean);
            }

            if (list_resp_bean.Count == 0 & (err_resp_bean != null))
                list_resp_bean.Add(err_resp_bean);
//            Debug.WriteLine(list_resp_bean[0].value);
            return list_resp_bean;

        }

        /// <summary>
        /// 自動翻訳
        /// </summary>
        /// <returns></returns>
        /// <remarks></remarks>
        static internal APIResponseBean get_auto_trans(GetAutoTransInfo search_bean)
        {
            try
            {
                APIResponseBean resp_bean = APIAccessor_ja.get_data_from_server(search_bean);
                if (resp_bean.is_error) return resp_bean;
                resp_bean.value = GetAutoTransInfo.convert_from_server_response(resp_bean, search_bean.api_name);
                return resp_bean;

            }
            catch (Exception e)
            {
                return new APIResponseBean(e);
            }
        }

        /// <summary>
        /// 自動翻訳 Request Bean
        /// </summary>
        /// <remarks></remarks>
        public class GetAutoTransInfo : ServerInfoBean
        {

            public void set_url(string url)
            {
                this.url = url;
            }

            public string text { get; set; }

            public string api_name;

            /// <summary>
            /// サーバレスポンスを変換
            /// </summary>
            /// <param name="resp_bean"></param>
            /// <returns></returns>
            /// <remarks></remarks>
            static internal AutoTransInfo convert_from_server_response(APIResponseBean resp_bean, string api_name)
            {

                AutoTransInfo bean = new AutoTransInfo();
                XmlDocument doc = APIAccessor_ja.create_XML_doc_from_xml_text(resp_bean.response);

                bean.server_name = api_name;
                bean.text_source = doc.SelectSingleNode("/resultset/request/text").InnerText;
                bean.text_translated = resp_bean.result.SelectSingleNode("/result/text").InnerText;
                return bean;

            }

            /// <summary>
            /// ユーザが設定した機械翻訳サーバ情報を取得
            /// </summary>
            /// <param name="text"></param>
            /// <param name="lang_org"></param>
            /// <param name="lang_trans"></param>
            /// <returns></returns>
            static internal List<GetAutoTransInfo> get_all_auto_trans_info(string text, Language lang_org, Language lang_trans)
            {
                Debug.WriteLine("石橋つり");
                List<AutoTransAPIInfo> list_datas = AutoTransAPIInfo.get_auto_trans_info();
                List<GetAutoTransInfo> list_info = new List<GetAutoTransInfo>();

                foreach (AutoTransAPIInfo api_info in list_datas)
                {
                    if (!api_info.use) continue;
                    if (api_info.lang_org != lang_org) continue;
                    if (api_info.lang_trans != lang_trans) continue;

                    GetAutoTransInfo search_info = new GetAutoTransInfo();
                    list_info.Add(search_info);
                    search_info.set_url(api_info.url);
                    search_info.text = text;
                    search_info.api_name = api_info.name;
                }

                return list_info;

            }

        }

        /// <summary>
        /// 自動翻訳 API情報 Bean
        /// </summary>
        public class AutoTransAPIInfo
        {

            public bool use { get; set; }
            public string name { get; set; }
            public Language lang_org { get; set; }
            public Language lang_trans { get; set; }
            public string url { get; set; }

            /// <summary>
            /// ユーザが設定した機械翻訳サーバ情報を取得
            /// </summary>
            /// <param name="init"></param>
            /// <returns></returns>
            static internal List<AutoTransAPIInfo> get_auto_trans_info(bool init = false)
            {

                string settings = MySettings.API_自動翻訳;
                if (init || string.IsNullOrWhiteSpace(settings))
                    settings = AutoTransAPIInfo.init_settings();

                List<string[]> list_datas = get_tsv_unescaped(settings);
                List<AutoTransAPIInfo> list_info = new List<AutoTransAPIInfo>();

                foreach (string[] datas in list_datas)
                {
                    string url = datas[4];
                    if (!url.Contains(MySettings.API_URL)) continue;
                    AutoTransAPIInfo info = new AutoTransAPIInfo();
                    list_info.Add(info);

                    info.use = datas[0] == "1";
                    info.name = datas[1];
                    info.lang_org = (Language)int.Parse(datas[2]);
                    info.lang_trans = (Language)int.Parse(datas[3]);
                    info.url = datas[4];

                }

                return list_info;

            }

            private static string init_settings()
            {

                List<List<string>> list = new List<List<string>>();

                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 中国語(簡体字) - 日本語",
                    ((int)Language.cn).ToString(),
                    ((int)Language.ja).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/general_zh-CN_ja/"
                }));
                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 中国語(繁体字) - 日本語",
                    ((int)Language.tw).ToString(),
                    ((int)Language.ja).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/general_zh-TW_ja/"
                }));
                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 日本語 - 中国語(簡体字)",
                    ((int)Language.ja).ToString(),
                    ((int)Language.cn).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/general_ja_zh-CN/"
                }));
                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 日本語 - 中国語(繁体字)",
                    ((int)Language.ja).ToString(),
                    ((int)Language.tw).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/general_ja_zh-TW/"
                }));
                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 日本語 - 英語",
                    ((int)Language.ja).ToString(),
                    ((int)Language.en).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/general_ja_en/"
                }));
                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 日本語 - 韓国語",
                    ((int)Language.ja).ToString(),
                    ((int)Language.ko).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/general_ja_ko/"
                }));
                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 英語 - 日本語",
                    ((int)Language.en).ToString(),
                    ((int)Language.ja).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/generalNT_en_ja/"
                }));
                list.Add(new List<String>(new string[] {
                    "1",
                    "汎用 : 韓国語 - 日本語",
                    ((int)Language.ko).ToString(),
                    ((int)Language.ja).ToString(),
                    "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/api/mt/general_ko_ja/"
                }));

                return create_TSV(list);

            }

        }

        /// <summary>
        /// 自動翻訳 Response Bean
        /// </summary>
        public class AutoTransInfo
        {
            public string server_name { get; set; }
            public string text_source { get; set; }
            public string text_translated { get; set; }
        }

        #endregion

        public class APIResponseBean
        {


            private static Dictionary<string, string> table_code_message = new Dictionary<string, string>();
            static APIResponseBean()
            {
                Dictionary<string, string> table = table_code_message;
                table.Add("0", "成功");
                table.Add("500", "API keyエラー");
                table.Add("501", "nameエラー");
                table.Add("502", "リクエスト上限エラー(1000回 / 日)");
                table.Add("510", "OAuth認証エラー");
                table.Add("511", "OAuthヘッダエラー");
                table.Add("520", "アクセスURLエラー");
                table.Add("521", "アクセスURLエラー");
                table.Add("522", "リクエストkeyエラー");
                table.Add("523", "リクエストnameエラー");
                table.Add("524", "リクエストパラメータエラー");
                table.Add("530", "一覧取得権限エラー");
                table.Add("531", "一覧取得実行エラー");
                table.Add("532", "登録データ無し");
            }

            public APIResponseBean()
            {
            }

            public const String ERROR_CD_NOT_FROM_SERVER = "100";

            public const String ERROR_CD_NO_RESULT = "532";
            public const String ERROR_CD_NO_DIC = "10001";

            /// <summary>
            /// VB例外用
            /// </summary>
            /// <param name="excep"></param>
            /// <remarks></remarks>
            public APIResponseBean(Exception excep) : this()
            {
                this.code = ERROR_CD_NOT_FROM_SERVER;
                this.error_message = excep.Message;
                this.exception = excep;
                this.is_error = true;
            }

            /// <summary>
            ///
            /// </summary>
            /// <param name="response"></param>
            /// <param name="is_xml">is_xml = false でも、サーバエラーはXMLで通知される</param>
            /// <remarks></remarks>
            public APIResponseBean(string response, bool is_xml = true)
            {
                this.response = response;

                try
                {
                    if (string.IsNullOrWhiteSpace(response))
                        this.exception = new Exception("response 空文字");
                    XmlDocument doc = APIAccessor_ja.create_XML_doc_from_xml_text(response, is_xml);
                    if (doc == null)
                    {
                        if (is_xml)
                        {
                            this.exception = new Exception("response xml 異常");
                        }
                        else
                        {
                            return;
                        }
                    }
                    if ((this.exception != null))
                    {
                        this.is_error = true;
                        this.message_processing = this.exception.Message;
                        return;
                    }

                    XmlElement elm_code = (XmlElement)doc.SelectSingleNode("/resultset/code");
                    this.code = elm_code.InnerText;
                    this.is_error = this.code != "0";
                    // 0:成功
                    if (this.is_error)
                        this.error_message = this.code_message;

                    XmlElement elm_message = (XmlElement)doc.SelectSingleNode("/resultset/message");
                    if ((elm_message != null))
                        this.message_processing = elm_message.InnerText;
                    XmlElement elm_result = (XmlElement)doc.SelectSingleNode("/resultset/result");
                    if ((elm_result != null))
                        this.result = APIAccessor_ja.create_XML_doc_from_xml_text(elm_result.OuterXml);
                }
                catch (Exception ex)
                {
                    this.is_error = true;
                    this.message_processing = "APIのレスポンスに異常があります。\n" + ex.Message;
                }
            }

            public string code { get; set; }

            public string code_message
            {
                get
                {
                    Dictionary<string, string> table = APIResponseBean.table_code_message;
                    if (string.IsNullOrWhiteSpace(code)) return "エラーコード無し";
                    if (!table.ContainsKey(code)) return "該当エラーメッセージ無し";
                    return APIResponseBean.table_code_message[code];
                }
            }

            public bool is_error { get; set; }
            public string message_processing { get; set; }
            public string response { get; set; }
            public XmlDocument result { get; set; }
            public Exception exception { get; set; }
            public object value { get; set; }

            private string _error_message;
            public string error_message
            {
                get { return this._error_message; }
                set
                {
                    this._error_message = value;
                    this.is_error = true;
                }
            }

            public static bool is_err(APIResponseBean res_info)
            {
                return (res_info != null) && res_info.is_error;
            }

            public void clear_error_info()
            {
                this.is_error = false;
                this._error_message = null;
                this.code = null;
                this.message_processing = null;
            }

        }

        /// <summary>
        /// サーバーへのリクエスト、レスポンス
        /// </summary>
        /// <param name="bean"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        static internal APIResponseBean get_data_from_server(ServerInfoBean bean, bool is_xml = true)
        {
            try
            {
                ProxyType proxy_type = string.IsNullOrWhiteSpace(bean.proxy) ? ProxyType.IE : ProxyType.Specified;

                HttpConnection.InitializeConnection(30, proxy_type, bean.proxy, bean.proxy_port, bean.proxy_id, bean.proxy_password);
                HttpConnection httpCon = new HttpConnection();
                httpCon.Initialize(MySettings.API_KEY, MySettings.API_SECRET, "", "", "");

                Dictionary<string, string> param = APIAccessor_ja.get_param_bean_web_client(bean);
                string content = "";
                httpCon.GetContent("POST", new Uri(bean.url), param, ref content, null);

                APIResponseBean resp_bean = new APIResponseBean(content, is_xml);
                if (resp_bean.code == APIResponseBean.ERROR_CD_NO_RESULT)
                    resp_bean.is_error = false;
                // 登録データがゼロ
                return resp_bean;

            }
            catch (WebException ex)
            {
                throw (new WebException("サーバーの接続に失敗しました。\n\n" + ex.Message));
            }

        }

        static string[] PARAM_UN_POST = new string[] {
            "url",
            "proxy",
            "proxy_id",
            "proxy_password",
            "proxy_port",
            "proxy_url"
        };

        /// <summary>
        /// 情報クラスからパラメータクラスに変換
        /// </summary>
        /// <param name="bean"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        private static Dictionary<string, string> get_param_bean_web_client(ServerInfoBean bean, bool ignore_blank = false)
        {

            Dictionary<string, string> @params = new Dictionary<string, string>();

            foreach (PropertyInfo prop in bean.GetType().GetProperties())
            {
                if (!prop.CanRead)
                    continue;
                object value_obj = prop.GetValue(bean, null);
                String value = value_obj != null ? value_obj.ToString() : "";
                if (ignore_blank && string.IsNullOrEmpty(value))
                    continue;
                if (!string.IsNullOrWhiteSpace(value))
                    @params.Add(prop.Name, value);

            }

            foreach (String param_name in PARAM_UN_POST)
            {
                @params.Remove(param_name);
            }

            return @params;

        }

        /// <summary>
        /// リクエスト情報beanの基底クラス
        /// </summary>
        public class ServerInfoBean
        {

            private string _url;
            public string url
            {
                get { return this._url; }
                protected set { this._url = value; }
            }

            public string name
            {
                get { return MySettings.API_USER; }
            }

            public string key
            {
                get { return MySettings.API_KEY; }
            }

            public string type
            {
                get { return "xml"; }
            }

            public string proxy
            {
                get { return MySettings.proxy; }
            }

            public int proxy_port
            {
                get { return MySettings.proxy_port; }
            }

            public string proxy_url
            {
                get
                {
                    string p_url = this.proxy;
                    string p_port = this.proxy_port.ToString();
                    if (!string.IsNullOrWhiteSpace(p_url) && !string.IsNullOrWhiteSpace(p_port))
                        p_url += ":" + p_port;
                    return p_url;
                }
            }

            public string proxy_id
            {
                get { return MySettings.proxy_id; }
            }

            public string proxy_password
            {
                get { return MySettings.proxy_password; }
            }

            protected void set_api_url(string cd_api)
            {
                this._url = "https://" + MySettings.API_URL + "/api/" + cd_api + "/";
            }

        }

        #region "Constants"

        private static Cutra.Properties.Settings MySettings = Cutra.Properties.Settings.Default;

        public enum Language : int
        {
            none = -1,
            ja = 0,        // 日本語
            en = 1,        // 英語
            cn = 2,        // 中国語（簡体）
            tw = 3,        // 中国語（繁体）
            ko = 4,        // 韓国語
        }

        public static string get_lang_cd(Language lang)
        {
            if (lang == Language.none) return "";

            string lang_cd = Enum.GetName(typeof(Language), lang);
            return lang_cd;
        }

        #endregion

        #region "その他"

        /// <summary>
        /// 文字列XML情報をXMLDocumentインスタンスに変換
        /// </summary>
        /// <param name="xml_text"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static XmlDocument create_XML_doc_from_xml_text(string xml_text, bool show_message = true)
        {

            XmlDocument xdoc = new XmlDocument();
            try
            {
                xdoc.LoadXml(xml_text);
            }
            catch (Exception e)
            {
                if (show_message) MessageBox.Show("XMLの読込に失敗しました。\n\n" + e.Message);
                return null;
            }

            return xdoc;

        }

        public static String create_TSV(List<List<String>> list_datas, bool remove_tab = false)
        {

            List<String> list = new List<String>();

            foreach (List<String> list_data in list_datas)
            {
                List<String> line_datas = new List<String>();
                foreach (String dt in list_data)
                {
                    if (remove_tab) dt.Replace("\t", "");
                    line_datas.Add(dt);
                }

                list.Add(string.Join("\t", line_datas.ToArray()));

            }
            return list.Count != 0 ? string.Join("\n", list.ToArray()) + "\n" : "";

        }


        public static List<String[]> get_tsv_unescaped(String str_tsv)
        {

            List<String[]> list_split = new List<String[]>();
            if (String.IsNullOrWhiteSpace(str_tsv))
                return list_split;

            foreach (String line in str_tsv.Split("\n".ToCharArray()))
            {
                String str_rep = Regex.Replace(line, "\\r$", "");
                if (String.IsNullOrWhiteSpace(str_rep)) continue;
                list_split.Add(Regex.Split(str_rep, "\\t"));
            }

            return list_split;

        }

        #endregion

        #endregion

        //AOuth認証のプログラムは複雑なので、
        //以下のregionをコピペで使用するのが良い
        #region "AOuth認証"

        public class HttpConnection
        {

            public enum ProxyType
            {
                None,
                IE,
                Specified
            }

            ///<summary>
            ///プロキシ
            ///</summary>

            private static WebProxy proxy = null;
            ///<summary>
            ///ユーザーが選択したプロキシの方式
            ///</summary>

            private static ProxyType proxyKind = ProxyType.IE;
            ///<summary>
            ///クッキー保存用コンテナ
            ///</summary>

            private static CookieContainer cookieContainer = new CookieContainer();
            ///<summary>
            ///初期化済みフラグ
            ///</summary>

            private static bool isInitialize = false;
            ///<summary>
            ///HttpWebRequestオブジェクトを取得する。パラメータはGET/HEAD/DELETEではクエリに、POST/PUTではエンティティボディに変換される。
            ///</summary>
            ///<remarks>
            ///追加で必要となるHTTPヘッダや通信オプションは呼び出し元で付加すること
            ///（Timeout,AutomaticDecompression,AllowAutoRedirect,UserAgent,ContentType,Accept,HttpRequestHeader.Authorization,カスタムヘッダ）
            ///POST/PUTでクエリが必要な場合は、requestUriに含めること。
            ///</remarks>
            ///<param name="method">HTTP通信メソッド（GET/HEAD/POST/PUT/DELETE）</param>
            ///<param name="requestUri">通信先URI</param>
            ///<param name="param">GET時のクエリ、またはPOST時のエンティティボディ</param>
            ///<param name="withCookie">通信にcookieを使用するか</param>
            ///<returns>引数で指定された内容を反映したHttpWebRequestオブジェクト</returns>
            protected HttpWebRequest CreateRequest(string method, Uri requestUri, Dictionary<string, string> param, bool withCookie)
            {
                if (!isInitialize)
                    throw new Exception("Sequence error.(not initialized)");

                //GETメソッドの場合はクエリとurlを結合
                UriBuilder ub = new UriBuilder(requestUri.AbsoluteUri);
                if (method == "GET" || method == "DELETE" || method == "HEAD")
                {
                    ub.Query = CreateQueryString(param);
                }

                HttpWebRequest webReq = (HttpWebRequest)WebRequest.Create(ub.Uri);

                //プロキシ設定
                if (proxyKind != ProxyType.IE)
                    webReq.Proxy = proxy;

                webReq.Method = method;
                if (method == "POST" || method == "PUT")
                {
                    webReq.ContentType = "application/x-www-form-urlencoded";
                    //POST/PUTメソッドの場合は、ボディデータとしてクエリ構成して書き込み
                    if ((param != null))
                    {
                        using (StreamWriter writer = new StreamWriter(webReq.GetRequestStream()))
                        {
                            writer.Write(CreateQueryString(param));
                        }
                    }
                }
                //cookie設定
                if (withCookie)
                    webReq.CookieContainer = cookieContainer;
                //タイムアウト設定
                webReq.Timeout = DefaultTimeout;

                return webReq;
            }

            ///<summary>
            ///HTTPの応答を処理し、応答ボディデータをテキストとして返却する
            ///</summary>
            ///<remarks>
            ///リダイレクト応答の場合（AllowAutoRedirect=Falseの場合のみ）は、headerInfoインスタンスがあればLocationを追加してリダイレクト先を返却
            ///WebExceptionはハンドルしていないので、呼び出し元でキャッチすること
            ///テキストの文字コードはUTF-8を前提として、エンコードはしていません
            ///</remarks>
            ///<param name="webRequest">HTTP通信リクエストオブジェクト</param>
            ///<param name="contentText">[OUT]HTTP応答のボディデータ</param>
            ///<param name="headerInfo">[IN/OUT]HTTP応答のヘッダ情報。ヘッダ名をキーにして空データのコレクションを渡すことで、該当ヘッダの値をデータに設定して戻す</param>
            ///<param name="withCookie">通信にcookieを使用する</param>
            ///<returns>HTTP応答のステータスコード</returns>
            protected HttpStatusCode GetResponse(HttpWebRequest webRequest, ref string contentText, Dictionary<string, string> headerInfo, bool withCookie)
            {
                try
                {
                    using (HttpWebResponse webRes = (HttpWebResponse)webRequest.GetResponse())
                    {
                        HttpStatusCode statusCode = webRes.StatusCode;
                        //cookie保持
                        if (withCookie)
                            SaveCookie(webRes.Cookies);
                        //リダイレクト応答の場合は、リダイレクト先を設定
                        GetHeaderInfo(webRes, headerInfo);
                        //応答のストリームをテキストに書き出し
                        if (contentText == null)
                            throw new ArgumentNullException("contentText");
                        if (webRes.ContentLength > 0)
                        {
                            using (StreamReader sr = new StreamReader(webRes.GetResponseStream()))
                            {
                                contentText = sr.ReadToEnd();
                            }
                        }
                        return statusCode;
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        HttpWebResponse res = (HttpWebResponse)ex.Response;
                        return res.StatusCode;
                    }
                    throw ex;
                }
            }

            ///<summary>
            ///HTTPの応答を処理します。応答ボディデータが不要な用途向け。
            ///</summary>
            ///<remarks>
            ///リダイレクト応答の場合（AllowAutoRedirect=Falseの場合のみ）は、headerInfoインスタンスがあればLocationを追加してリダイレクト先を返却
            ///WebExceptionはハンドルしていないので、呼び出し元でキャッチすること
            ///</remarks>
            ///<param name="webRequest">HTTP通信リクエストオブジェクト</param>
            ///<param name="headerInfo">[IN/OUT]HTTP応答のヘッダ情報。ヘッダ名をキーにして空データのコレクションを渡すことで、該当ヘッダの値をデータに設定して戻す</param>
            ///<param name="withCookie">通信にcookieを使用する</param>
            ///<returns>HTTP応答のステータスコード</returns>
            protected HttpStatusCode GetResponse(HttpWebRequest webRequest, Dictionary<string, string> headerInfo, bool withCookie)
            {
                try
                {
                    using (HttpWebResponse webRes = (HttpWebResponse)webRequest.GetResponse())
                    {
                        HttpStatusCode statusCode = webRes.StatusCode;
                        //cookie保持
                        if (withCookie)
                            SaveCookie(webRes.Cookies);
                        //リダイレクト応答の場合は、リダイレクト先を設定
                        GetHeaderInfo(webRes, headerInfo);
                        return statusCode;
                    }
                }
                catch (WebException ex)
                {
                    if (ex.Status == WebExceptionStatus.ProtocolError)
                    {
                        HttpWebResponse res = (HttpWebResponse)ex.Response;
                        return res.StatusCode;
                    }
                    throw ex;
                }
            }

            ///<summary>
            ///クッキーを保存。ホスト名なしのドメインの場合、ドメイン名から先頭のドットを除去して追加しないと再利用されないため
            ///</summary>
            private void SaveCookie(CookieCollection cookieCollection)
            {
                foreach (Cookie ck in cookieCollection)
                {
                    if (ck.Domain.StartsWith("."))
                    {
                        ck.Domain = ck.Domain.Substring(1, ck.Domain.Length - 1);
                        cookieContainer.Add(ck);
                    }
                }
            }

            ///<summary>
            ///headerInfoのキー情報で指定されたHTTPヘッダ情報を取得・格納する。redirect応答時はLocationヘッダの内容を追記する
            ///</summary>
            ///<param name="webResponse">HTTP応答</param>
            ///<param name="headerInfo">[IN/OUT]キーにヘッダ名を指定したデータ空のコレクション。取得した値をデータにセットして戻す</param>

            private void GetHeaderInfo(HttpWebResponse webResponse, Dictionary<string, string> headerInfo)
            {
                if (headerInfo == null)
                    return;

                if (headerInfo.Count > 0)
                {
                    string[] keys = new string[headerInfo.Count];
                    headerInfo.Keys.CopyTo(keys, 0);
                    foreach (string key in keys)
                    {
                        if (Array.IndexOf(webResponse.Headers.AllKeys, key) > -1)
                        {
                            headerInfo[key] = webResponse.Headers[key];
                        }
                        else
                        {
                            headerInfo[key] = "";
                        }
                    }
                }

                HttpStatusCode statusCode = webResponse.StatusCode;
                if (statusCode == HttpStatusCode.MovedPermanently || statusCode == HttpStatusCode.Found || statusCode == HttpStatusCode.SeeOther || statusCode == HttpStatusCode.TemporaryRedirect)
                {
                    if (headerInfo.ContainsKey("Location"))
                    {
                        headerInfo["Location"] = webResponse.Headers["Location"];
                    }
                    else
                    {
                        headerInfo.Add("Location", webResponse.Headers["Location"]);
                    }
                }
            }

            ///<summary>
            ///クエリコレクションをkey=value形式の文字列に構成して戻す
            ///</summary>
            ///<param name="param">クエリ、またはポストデータとなるkey-valueコレクション</param>
            protected string CreateQueryString(IDictionary<string, string> param)
            {
                if (param == null || param.Count == 0)
                    return string.Empty;

                StringBuilder query = new StringBuilder();
                foreach (string key in param.Keys)
                {
                    query.AppendFormat("{0}={1}&", UrlEncode(key), UrlEncode(param[key]));
                }
                return query.ToString(0, query.Length - 1);
            }

            ///<summary>
            ///クエリ形式（key1=value1&amp;key2=value2&amp;...）の文字列をkey-valueコレクションに詰め直し
            ///</summary>
            ///<param name="queryString">クエリ文字列</param>
            ///<returns>key-valueのコレクション</returns>
            protected NameValueCollection ParseQueryString(string queryString)
            {
                NameValueCollection query = new NameValueCollection();
                string[] parts = queryString.Split('&');
                foreach (string part in parts)
                {
                    int index = part.IndexOf('=');
                    if (index == -1)
                    {
                        query.Add(Uri.UnescapeDataString(part), "");
                    }
                    else
                    {
                        query.Add(Uri.UnescapeDataString(part.Substring(0, index)), Uri.UnescapeDataString(part.Substring(index + 1)));
                    }
                }
                return query;
            }

            ///<summary>
            ///2バイト文字も考慮したUrlエンコード
            ///</summary>
            ///<param name="stringToEncode">エンコードする文字列</param>
            ///<returns>エンコード結果文字列</returns>
            protected string UrlEncode(string stringToEncode)
            {
                const string UnreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";
                StringBuilder sb = new StringBuilder();
                byte[] bytes = Encoding.UTF8.GetBytes(stringToEncode);

                foreach (byte b in bytes)
                {
                    if (UnreservedChars.IndexOf((char)b) != -1)
                    {
                        sb.Append((char)b);
                    }
                    else
                    {
                        sb.AppendFormat("%{0:X2}", b);
                    }
                }
                return sb.ToString();
            }

            #region "DefaultTimeout"
            ///<summary>
            ///通信タイムアウト時間（ms）
            ///</summary>

            private static int timeout = 20000;
            ///<summary>
            ///通信タイムアウト時間（ms）。10～120秒の範囲で指定。範囲外は20秒とする
            ///</summary>
            protected static int DefaultTimeout
            {
                get { return timeout; }
                set
                {
                    const int TimeoutMinValue = 10000;
                    const int TimeoutMaxValue = 120000;
                    const int TimeoutDefaultValue = 20000;
                    if (value < TimeoutMinValue || value > TimeoutMaxValue)
                    {
                        // 範囲外ならデフォルト値設定
                        timeout = TimeoutDefaultValue;
                    }
                    else
                    {
                        timeout = value;
                    }
                }
            }
            #endregion

            ///<summary>
            ///通信クラスの初期化処理。タイムアウト値とプロキシを設定する
            ///</summary>
            ///<remarks>
            ///通信開始前に最低一度呼び出すこと
            ///</remarks>
            ///<param name="timeout">タイムアウト値（秒）。10～120秒の範囲で指定。範囲外は20秒とする</param>
            ///<param name="proxyType">なし・指定・IEデフォルト</param>
            ///<param name="proxyAddress">プロキシのホスト名orIPアドレス</param>
            ///<param name="proxyPort">プロキシのポート番号</param>
            ///<param name="proxyUser">プロキシ認証が必要な場合のユーザ名。不要なら空文字</param>
            ///<param name="proxyPassword">プロキシ認証が必要な場合のパスワード。不要なら空文字</param>
            public static void InitializeConnection(int timeout, ProxyType proxyType, string proxyAddress, int proxyPort, string proxyUser, string proxyPassword)
            {
                isInitialize = true;
                ServicePointManager.Expect100Continue = false;
                DefaultTimeout = timeout * 1000;
                //s -> ms
                switch (proxyType)
                {
                    case ProxyType.None:
                        proxy = null;
                        break;
                    case ProxyType.Specified:
                        proxy = new WebProxy("http://" + proxyAddress + ":" + proxyPort.ToString());
                        if (!string.IsNullOrEmpty(proxyUser) || !string.IsNullOrEmpty(proxyPassword))
                        {
                            proxy.Credentials = new NetworkCredential(proxyUser, proxyPassword);
                        }
                        break;
                    case ProxyType.IE:
                        break;
                        //IE設定（システム設定）はデフォルト値なので処理しない
                }
            }

            /// <summary>
            /// OAuth署名のoauth_timestamp算出用基準日付（1970/1/1 00:00:00
            /// </summary>

            private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
            ///<summary>
            ///OAuth署名のoauth_nonce算出用乱数クラス
            ///</summary>

            private static readonly Random NonceRandom = new Random();
            ///<summary>
            ///OAuthのアクセストークン。永続化可能（ユーザー取り消しの可能性はある）。
            ///</summary>

            private string token = "";
            ///<summary>
            ///OAuthの署名作成用秘密アクセストークン。永続化可能（ユーザー取り消しの可能性はある）。
            ///</summary>

            private string tokenSecret = "";
            ///<summary>
            ///OAuthのコンシューマー鍵
            ///</summary>

            private string consumerKey;
            ///<summary>
            ///OAuthの署名作成用秘密コンシューマーデータ
            ///</summary>

            private string consumerSecret;
            ///<summary>
            ///認証成功時の応答でユーザー情報を取得する場合のキー。設定しない場合は、AuthUsernameもブランクのままとなる
            ///</summary>

            private string userIdentKey;
            ///<summary>
            ///OAuthの署名作成用秘密コンシューマーデータ
            ///</summary>

            private string authorizedUsername;
            ///<summary>
            ///OAuth認証で指定のURLとHTTP通信を行い、結果を返す
            ///</summary>
            ///<param name="method">HTTP通信メソッド（GET/HEAD/POST/PUT/DELETE）</param>
            ///<param name="requestUri">通信先URI</param>
            ///<param name="param">GET時のクエリ、またはPOST時のエンティティボディ</param>
            ///<param name="content">[OUT]HTTP応答のボディデータ</param>
            ///<param name="headerInfo">[IN/OUT]HTTP応答のヘッダ情報。必要なヘッダ名を事前に設定しておくこと</param>
            ///<returns>HTTP応答のステータスコード</returns>
            public HttpStatusCode GetContent(string method, Uri requestUri, Dictionary<string, string> param, ref string content, Dictionary<string, string> headerInfo)
            {

                HttpWebRequest webReq = CreateRequest(method, requestUri, param, false);
                //OAuth認証ヘッダを付加
                if (string.IsNullOrEmpty(token))
                {
                    AppendOAuthInfo(webReq, param, token, tokenSecret);
                }

                if (content == null)
                {
                    return GetResponse(webReq, headerInfo, false);
                }
                else
                {
                    return GetResponse(webReq, ref content, headerInfo, false);
                }
            }

            #region "認証処理"
            ///<summary>
            ///OAuth認証の開始要求（リクエストトークン取得）。PIN入力用の前段
            ///</summary>
            ///<remarks>
            ///呼び出し元では戻されたurlをブラウザで開き、認証完了後PIN入力を受け付けて、リクエストトークンと共にAuthenticatePinFlowを呼び出す
            ///</remarks>
            ///<param name="requestTokenUrl">リクエストトークンの取得先URL</param>
            ///<param name="authorizeUrl">ブラウザで開く認証用URLのベース</param>
            ///<param name="requestToken">[OUT]認証要求で戻されるリクエストトークン。使い捨て</param>
            ///<param name="authUri">[OUT]requestUriを元に生成された認証用URL。通常はリクエストトークンをクエリとして付加したUri</param>
            ///<returns>取得結果真偽値</returns>
            public bool AuthenticatePinFlowRequest(string requestTokenUrl, string authorizeUrl, ref string requestToken, ref Uri authUri)
            {
                //PIN-based flow
                authUri = GetAuthenticatePageUri(requestTokenUrl, authorizeUrl, ref requestToken);
                if (authUri == null)
                    return false;
                return true;
            }

            ///<summary>
            ///OAuth認証のアクセストークン取得。PIN入力用の後段
            ///</summary>
            ///<remarks>
            ///事前にAuthenticatePinFlowRequestを呼んで、ブラウザで認証後に表示されるPINを入力してもらい、その値とともに呼び出すこと
            ///</remarks>
            ///<param name="accessTokenUrl">アクセストークンの取得先URL</param>
            ///<param name="requestToken">AuthenticatePinFlowRequestで取得したリクエストトークン</param>
            ///<param name="pinCode">Webで認証後に表示されるPINコード</param>
            ///<returns>取得結果真偽値</returns>
            public bool AuthenticatePinFlow(string accessTokenUrl, string requestToken, string pinCode)
            {
                //PIN-based flow
                if (string.IsNullOrEmpty(requestToken))
                    throw new Exception("Sequence error.(requestToken is blank)");

                //アクセストークン取得
                NameValueCollection accessTokenData = GetOAuthToken(new Uri(accessTokenUrl), pinCode, requestToken, null);

                if (accessTokenData != null)
                {
                    token = accessTokenData["oauth_token"];
                    tokenSecret = accessTokenData["oauth_token_secret"];
                    //サービスごとの独自拡張対応
                    if (!string.IsNullOrEmpty(this.userIdentKey))
                    {
                        authorizedUsername = accessTokenData[this.userIdentKey];
                    }
                    else
                    {
                        authorizedUsername = "";
                    }
                    if (string.IsNullOrEmpty(token))
                        return false;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            ///<summary>
            ///OAuth認証のアクセストークン取得。xAuth方式
            ///</summary>
            ///<param name="accessTokenUrl">アクセストークンの取得先URL</param>
            ///<param name="username">認証用ユーザー名</param>
            ///<param name="password">認証用パスワード</param>
            ///<returns>取得結果真偽値</returns>
            public bool AuthenticateXAuth(string accessTokenUrl, string username, string password)
            {
                //ユーザー・パスワードチェック
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    throw new Exception("Sequence error.(username or password is blank)");
                }
                //xAuthの拡張パラメータ設定
                Dictionary<string, string> parameter = new Dictionary<string, string>();
                parameter.Add("x_auth_mode", "client_auth");
                parameter.Add("x_auth_username", username);
                parameter.Add("x_auth_password", password);

                //アクセストークン取得
                NameValueCollection accessTokenData = GetOAuthToken(new Uri(accessTokenUrl), "", "", parameter);

                if (accessTokenData != null)
                {
                    token = accessTokenData["oauth_token"];
                    tokenSecret = accessTokenData["oauth_token_secret"];
                    //サービスごとの独自拡張対応
                    if (!string.IsNullOrEmpty(this.userIdentKey))
                    {
                        authorizedUsername = accessTokenData[this.userIdentKey];
                    }
                    else
                    {
                        authorizedUsername = "";
                    }
                    if (string.IsNullOrEmpty(token))
                        return false;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            ///<summary>
            ///OAuth認証のリクエストトークン取得。リクエストトークンと組み合わせた認証用のUriも生成する
            ///</summary>
            ///<param name="requestTokenUrl">リクエストトークンの取得先URL</param>
            ///<param name="authorizeUrl">ブラウザで開く認証用URLのベース</param>
            ///<param name="requestToken">[OUT]取得したリクエストトークン</param>
            ///<returns>取得結果真偽値</returns>
            private Uri GetAuthenticatePageUri(string requestTokenUrl, string authorizeUrl, ref string requestToken)
            {
                const string tokenKey = "oauth_token";

                //リクエストトークン取得
                NameValueCollection reqTokenData = GetOAuthToken(new Uri(requestTokenUrl), "", "", null);
                if (reqTokenData != null)
                {
                    requestToken = reqTokenData[tokenKey];
                    //Uri生成
                    UriBuilder ub = new UriBuilder(authorizeUrl);
                    ub.Query = string.Format("{0}={1}", tokenKey, requestToken);
                    return ub.Uri;
                }
                else
                {
                    return null;
                }
            }

            ///<summary>
            ///OAuth認証のトークン取得共通処理
            ///</summary>
            ///<param name="requestUri">各種トークンの取得先URL</param>
            ///<param name="pinCode">PINフロー時のアクセストークン取得時に設定。それ以外は空文字列</param>
            ///<param name="requestToken">PINフロー時のリクエストトークン取得時に設定。それ以外は空文字列</param>
            ///<param name="parameter">追加パラメータ。xAuthで使用</param>
            ///<returns>取得結果のデータ。正しく取得出来なかった場合はNothing</returns>
            private NameValueCollection GetOAuthToken(Uri requestUri, string pinCode, string requestToken, Dictionary<string, string> parameter)
            {
                HttpWebRequest webReq = null;
                //HTTPリクエスト生成。PINコードもパラメータも未指定の場合はGETメソッドで通信。それ以外はPOST
                if (string.IsNullOrEmpty(pinCode) && parameter == null)
                {
                    webReq = CreateRequest("GET", requestUri, null, false);
                }
                else
                {
                    webReq = CreateRequest("POST", requestUri, parameter, false);
                    //ボディに追加パラメータ書き込み
                }
                //OAuth関連パラメータ準備。追加パラメータがあれば追加
                Dictionary<string, string> query = new Dictionary<string, string>();
                if (parameter != null)
                {
                    foreach (KeyValuePair<string, string> kvp in parameter)
                    {
                        query.Add(kvp.Key, kvp.Value);
                    }
                }
                //PINコードが指定されていればパラメータに追加
                if (!string.IsNullOrEmpty(pinCode))
                    query.Add("oauth_verifier", pinCode);
                //OAuth関連情報をHTTPリクエストに追加
                AppendOAuthInfo(webReq, query, requestToken, "");
                //HTTP応答取得
                try
                {
                    string contentText = "";
                    HttpStatusCode status = GetResponse(webReq, ref contentText, null, false);
                    if (status == HttpStatusCode.OK)
                    {
                        return ParseQueryString(contentText);
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print(ex.ToString());
                    return null;
                }
            }
            #endregion

            #region "OAuth認証用ヘッダ作成・付加処理"
            ///<summary>
            ///HTTPリクエストにOAuth関連ヘッダを追加
            ///</summary>
            ///<param name="webRequest">追加対象のHTTPリクエスト</param>
            ///<param name="query">OAuth追加情報＋クエリ or POSTデータ</param>
            ///<param name="token">アクセストークン、もしくはリクエストトークン。未取得なら空文字列</param>
            ///<param name="tokenSecret">アクセストークンシークレット。認証処理では空文字列</param>
            private void AppendOAuthInfo(HttpWebRequest webRequest, Dictionary<string, string> query, string token, string tokenSecret)
            {
                //OAuth共通情報取得
                Dictionary<string, string> parameter = GetOAuthParameter(token);
                //OAuth共通情報にquery情報を追加
                if (query != null)
                {
                    foreach (KeyValuePair<string, string> item in query)
                    {
                        parameter.Add(item.Key, item.Value);
                    }
                }
                //署名の作成・追加
                parameter.Add("oauth_signature", CreateSignature(tokenSecret, webRequest.Method, webRequest.RequestUri, parameter));
                //HTTPリクエストのヘッダに追加
                StringBuilder sb = new StringBuilder("OAuth ");
                foreach (KeyValuePair<string, string> item in parameter)
                {
                    //各種情報のうち、oauth_で始まる情報のみ、ヘッダに追加する。各情報はカンマ区切り、データはダブルクォーテーションで括る
                    if (item.Key.StartsWith("oauth_"))
                    {
                        sb.AppendFormat("{0}=\"{1}\",", item.Key, UrlEncode(item.Value));
                    }
                }
                webRequest.Headers.Add(HttpRequestHeader.Authorization, sb.ToString());
            }

            ///<summary>
            ///OAuthで使用する共通情報を取得する
            ///</summary>
            ///<param name="token">アクセストークン、もしくはリクエストトークン。未取得なら空文字列</param>
            ///<returns>OAuth情報のディクショナリ</returns>
            private Dictionary<string, string> GetOAuthParameter(string token)
            {
                Dictionary<string, string> parameter = new Dictionary<string, string>();
                parameter.Add("oauth_consumer_key", consumerKey);
                parameter.Add("oauth_signature_method", "HMAC-SHA1");
                parameter.Add("oauth_timestamp", Convert.ToInt64((DateTime.UtcNow - UnixEpoch).TotalSeconds).ToString());
                //epoch秒
                parameter.Add("oauth_nonce", NonceRandom.Next(123400, 9999999).ToString());
                parameter.Add("oauth_version", "1.0");
                if (!string.IsNullOrEmpty(token))
                    parameter.Add("oauth_token", token);
                //トークンがあれば追加
                return parameter;
            }

            ///<summary>
            ///OAuth認証ヘッダの署名作成
            ///</summary>
            ///<param name="tokenSecret">アクセストークン秘密鍵</param>
            ///<param name="method">HTTPメソッド文字列</param>
            ///<param name="uri">アクセス先Uri</param>
            ///<param name="parameter">クエリ、もしくはPOSTデータ</param>
            ///<returns>署名文字列</returns>
            private string CreateSignature(string tokenSecret, string method, Uri uri, Dictionary<string, string> parameter)
            {
                //パラメタをソート済みディクショナリに詰替（OAuthの仕様）
                SortedDictionary<string, string> sorted = new SortedDictionary<string, string>(parameter);
                //URLエンコード済みのクエリ形式文字列に変換
                string paramString = CreateQueryString(sorted);
                //アクセス先URLの整形
                string url = string.Format("{0}://{1}{2}", uri.Scheme, uri.Host, uri.AbsolutePath);
                //署名のベース文字列生成（&区切り）。クエリ形式文字列は再エンコードする
                string signatureBase = string.Format("{0}&{1}&{2}", method, UrlEncode(url), UrlEncode(paramString));
                //署名鍵の文字列をコンシューマー秘密鍵とアクセストークン秘密鍵から生成（&区切り。アクセストークン秘密鍵なくても&残すこと）
                string key = UrlEncode(consumerSecret) + "&";
                if (!string.IsNullOrEmpty(tokenSecret))
                    key += UrlEncode(tokenSecret);
                //鍵生成＆署名生成
                System.Security.Cryptography.HMACSHA1 hmac = new System.Security.Cryptography.HMACSHA1(Encoding.ASCII.GetBytes(key));
                byte[] hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(signatureBase));
                return Convert.ToBase64String(hash);
            }

            #endregion

            ///<summary>
            ///初期化。各種トークンの設定とユーザー識別情報設定
            ///</summary>
            ///<param name="consumerKey">コンシューマー鍵</param>
            ///<param name="consumerSecret">コンシューマー秘密鍵</param>
            ///<param name="accessToken">アクセストークン</param>
            ///<param name="accessTokenSecret">アクセストークン秘密鍵</param>
            ///<param name="userIdentifier">アクセストークン取得時に得られるユーザー識別情報。不要なら空文字列</param>
            public void Initialize(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret, string userIdentifier)
            {
                this.consumerKey = consumerKey;
                this.consumerSecret = consumerSecret;
                this.token = accessToken;
                this.tokenSecret = accessTokenSecret;
                this.userIdentKey = userIdentifier;
            }

            ///<summary>
            ///初期化。各種トークンの設定とユーザー識別情報設定
            ///</summary>
            ///<param name="consumerKey">コンシューマー鍵</param>
            ///<param name="consumerSecret">コンシューマー秘密鍵</param>
            ///<param name="accessToken">アクセストークン</param>
            ///<param name="accessTokenSecret">アクセストークン秘密鍵</param>
            ///<param name="username">認証済みユーザー名</param>
            ///<param name="userIdentifier">アクセストークン取得時に得られるユーザー識別情報。不要なら空文字列</param>
            public void Initialize(string consumerKey, string consumerSecret, string accessToken, string accessTokenSecret, string username, string userIdentifier)
            {
                Initialize(consumerKey, consumerSecret, accessToken, accessTokenSecret, userIdentifier);
                authorizedUsername = username;
            }

            ///<summary>
            ///アクセストークン
            ///</summary>
            public string AccessToken
            {
                get { return token; }
            }

            ///<summary>
            ///アクセストークン秘密鍵
            ///</summary>
            public string AccessTokenSecret
            {
                get { return tokenSecret; }
            }

            ///<summary>
            ///認証済みユーザー名
            ///</summary>
            public string AuthUsername
            {
                get { return authorizedUsername; }
            }
        }

        #endregion

    }

}