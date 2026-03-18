using System.Collections.Generic;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Static lookup tables for Chinese (Mandarin) phonemization.
    /// Contains pinyin initial/final to IPA mappings, PUA token mappings,
    /// punctuation mappings, and character-to-pinyin lookup data.
    /// </summary>
    public static class PinyinData
    {
        // =====================================================================
        // Pinyin Initial -> IPA mapping
        // In Mandarin phonology, pinyin letters map differently from English:
        // b=[p], p=[ph], d=[t], t=[th], g=[k], k=[kh] (aspiration distinction)
        // =====================================================================
        public static readonly Dictionary<string, string> InitialToIpa = new()
        {
            { "b", "p" },
            { "p", "p\u02b0" },       // ph
            { "m", "m" },
            { "f", "f" },
            { "d", "t" },
            { "t", "t\u02b0" },       // th
            { "n", "n" },
            { "l", "l" },
            { "g", "k" },
            { "k", "k\u02b0" },       // kh
            { "h", "x" },
            { "j", "t\u0255" },       // tc (alveolopalatal)
            { "q", "t\u0255\u02b0" }, // tch (alveolopalatal aspirated)
            { "x", "\u0255" },        // c (alveolopalatal fricative)
            { "zh", "t\u0282" },      // ts (retroflex affricate)
            { "ch", "t\u0282\u02b0" }, // tsh (retroflex aspirated)
            { "sh", "\u0282" },       // s (retroflex fricative)
            { "r", "\u027b" },        // r (retroflex approximant)
            { "z", "ts" },
            { "c", "ts\u02b0" },      // tsh (alveolar aspirated)
            { "s", "s" },
        };

        // =====================================================================
        // Pinyin Final -> IPA mapping (compound finals as single tokens)
        // =====================================================================
        public static readonly Dictionary<string, string> FinalToIpa = new()
        {
            // Simple vowels
            { "a", "a" },
            { "o", "o" },
            { "e", "\u0264" },          // open-mid back unrounded vowel
            { "i", "i" },
            { "u", "u" },
            { "\u00fc", "y_vowel" },    // u-umlaut -> y_vowel
            { "v", "y_vowel" },
            // Diphthongs
            { "ai", "a\u026a" },        // ai
            { "ei", "e\u026a" },        // ei
            { "ao", "a\u028a" },        // au
            { "ou", "o\u028a" },        // ou
            // Nasal finals
            { "an", "an" },
            { "en", "\u0259n" },        // schwa-n
            { "ang", "a\u014b" },       // a-ng
            { "eng", "\u0259\u014b" },  // schwa-ng
            { "ong", "u\u014b" },       // u-ng
            // Retroflex final
            { "er", "\u025a" },         // r-colored schwa
            // i-compound finals (qi chi hu)
            { "ia", "ia" },
            { "ie", "i\u025b" },        // i-open_e
            { "iao", "ia\u028a" },      // i-a-u
            { "iu", "iou" },
            { "iou", "iou" },
            { "ian", "i\u025bn" },      // i-open_e-n
            { "in", "in" },
            { "iang", "ia\u014b" },     // i-a-ng
            { "ing", "i\u014b" },       // i-ng
            { "iong", "iu\u014b" },     // i-u-ng
            // u-compound finals (he kou hu)
            { "ua", "ua" },
            { "uo", "uo" },
            { "uai", "ua\u026a" },      // u-a-i
            { "ui", "ue\u026a" },       // u-e-i
            { "uei", "ue\u026a" },      // u-e-i (alternate spelling)
            { "uan", "uan" },
            { "un", "u\u0259n" },       // u-schwa-n
            { "uen", "u\u0259n" },      // u-schwa-n (alternate spelling)
            { "uang", "ua\u014b" },     // u-a-ng
            { "ueng", "u\u0259\u014b" }, // u-schwa-ng
            // u-umlaut-compound finals (cuo kou hu)
            { "\u00fce", "y\u025b" },   // u-umlaut-e -> y-open_e
            { "ve", "y\u025b" },
            { "\u00fcan", "y\u025bn" }, // u-umlaut-an -> y-open_e-n
            { "van", "y\u025bn" },
            { "\u00fcn", "yn" },        // u-umlaut-n -> yn
            { "vn", "yn" },
            // Syllabic consonants (internal keys set by SplitPinyin)
            { "-i_retroflex", "\u027b\u0329" }, // syllabic retroflex
            { "-i_alveolar", "\u0268" },        // close central unrounded vowel
        };

        // =====================================================================
        // PUA (Private Use Area) token mapping for multi-character IPA tokens
        // Must match token_mapper.py FIXED_PUA_MAPPING
        // =====================================================================
        public static readonly Dictionary<string, char> IpaToPua = new()
        {
            // --- Initials (aspirated/affricate) ---
            { "p\u02b0", '\uE020' },          // ph
            { "t\u02b0", '\uE021' },          // th
            { "k\u02b0", '\uE022' },          // kh
            { "t\u0255", '\uE023' },          // tc (alveolopalatal)
            { "t\u0255\u02b0", '\uE024' },    // tch (alveolopalatal aspirated)
            { "t\u0282", '\uE025' },          // ts (retroflex)
            { "t\u0282\u02b0", '\uE026' },    // tsh (retroflex aspirated)
            { "ts\u02b0", '\uE027' },         // tsh (alveolar aspirated)
            // --- Diphthongs ---
            { "a\u026a", '\uE028' },          // ai
            { "e\u026a", '\uE029' },          // ei
            { "a\u028a", '\uE02A' },          // au
            { "o\u028a", '\uE02B' },          // ou
            // --- Nasal finals ---
            { "an", '\uE02C' },
            { "\u0259n", '\uE02D' },          // schwa-n
            { "a\u014b", '\uE02E' },          // a-ng
            { "\u0259\u014b", '\uE02F' },     // schwa-ng
            { "u\u014b", '\uE030' },          // u-ng
            // --- i-compound finals ---
            { "ia", '\uE031' },
            { "i\u025b", '\uE032' },          // i-open_e
            { "iou", '\uE033' },
            { "ia\u028a", '\uE034' },         // i-a-u
            { "i\u025bn", '\uE035' },         // i-open_e-n
            { "in", '\uE036' },
            { "ia\u014b", '\uE037' },         // i-a-ng
            { "i\u014b", '\uE038' },          // i-ng
            { "iu\u014b", '\uE039' },         // i-u-ng
            // --- u-compound finals ---
            { "ua", '\uE03A' },
            { "uo", '\uE03B' },
            { "ua\u026a", '\uE03C' },         // u-a-i
            { "ue\u026a", '\uE03D' },         // u-e-i
            { "uan", '\uE03E' },
            { "u\u0259n", '\uE03F' },         // u-schwa-n
            { "ua\u014b", '\uE040' },         // u-a-ng
            { "u\u0259\u014b", '\uE041' },    // u-schwa-ng
            // --- u-umlaut-compound finals ---
            { "y\u025b", '\uE042' },          // y-open_e
            { "y\u025bn", '\uE043' },         // y-open_e-n
            { "yn", '\uE044' },
            // --- Syllabic consonants ---
            { "\u027b\u0329", '\uE045' },     // syllabic retroflex
            // --- Tone markers ---
            { "tone1", '\uE046' },
            { "tone2", '\uE047' },
            { "tone3", '\uE048' },
            { "tone4", '\uE049' },
            { "tone5", '\uE04A' },
            // --- Shared multilingual ---
            { "y_vowel", '\uE01E' },
            // --- ts (alveolar affricate, shared with JA) ---
            { "ts", '\uE00F' },
        };

        // =====================================================================
        // Chinese punctuation -> Western equivalent mapping
        // =====================================================================
        public static readonly Dictionary<char, char> PunctuationMap = new()
        {
            { '\u3002', '.' },  // fullwidth period
            { '\uff0c', ',' },  // fullwidth comma
            { '\uff01', '!' },  // fullwidth exclamation
            { '\uff1f', '?' },  // fullwidth question
            { '\u3001', ',' },  // ideographic comma
            { '\uff1b', ';' },  // fullwidth semicolon
            { '\uff1a', ':' },  // fullwidth colon
            { '\u2026', '.' },  // ellipsis
            { '\u2014', ',' },  // em-dash -> pause
            { '\u201c', '"' },  // left curly double quote
            { '\u201d', '"' },  // right curly double quote
            { '\u2018', '\'' }, // left curly single quote
            { '\u2019', '\'' }, // right curly single quote
        };

        // =====================================================================
        // Set of all punctuation characters (Chinese + Western)
        // =====================================================================
        public static readonly HashSet<char> PunctuationSet = new()
        {
            ',', '.', ';', ':', '!', '?',
            '\u3002', '\uff0c', '\uff01', '\uff1f', '\u3001',
            '\uff1b', '\uff1a', '\u201c', '\u201d', '\u2018',
            '\u2019', '\u2026', '\u2014',
        };

        // =====================================================================
        // Ordered list of consonant initials (two-char first for prefix matching)
        // =====================================================================
        public static readonly string[] InitialsOrder =
        {
            "zh", "ch", "sh",
            "b", "p", "m", "f",
            "d", "t", "n", "l",
            "g", "k", "h",
            "j", "q", "x",
            "r", "z", "c", "s",
        };

        // =====================================================================
        // Retroflex and alveolar initial sets for syllabic consonant detection
        // =====================================================================
        public static readonly HashSet<string> RetroflexInitials = new()
        {
            "zh", "ch", "sh", "r",
        };

        public static readonly HashSet<string> AlveolarInitials = new()
        {
            "z", "c", "s",
        };

        // =====================================================================
        // Character -> Pinyin lookup table
        // Covers the most common ~3500 Chinese characters by usage frequency.
        // Pinyin is given with tone number suffix (1-5).
        // For polyphones, the most common reading is provided.
        // =====================================================================
        public static readonly Dictionary<char, string> CharToPinyin = new()
        {
            // ---- Top 100 most frequent characters ----
            { '\u7684', "de5" },    // 的
            { '\u4e00', "yi1" },    // 一
            { '\u662f', "shi4" },   // 是
            { '\u4e0d', "bu4" },    // 不
            { '\u4e86', "le5" },    // 了
            { '\u4eba', "ren2" },   // 人
            { '\u6211', "wo3" },    // 我
            { '\u5728', "zai4" },   // 在
            { '\u6709', "you3" },   // 有
            { '\u4ed6', "ta1" },    // 他
            { '\u8fd9', "zhe4" },   // 这
            { '\u4e2d', "zhong1" }, // 中
            { '\u5927', "da4" },    // 大
            { '\u6765', "lai2" },   // 来
            { '\u4e0a', "shang4" }, // 上
            { '\u56fd', "guo2" },   // 国
            { '\u4e2a', "ge4" },    // 个
            { '\u5230', "dao4" },   // 到
            { '\u8bf4', "shuo1" },  // 说
            { '\u4eec', "men5" },   // 们
            { '\u4e3a', "wei4" },   // 为
            { '\u5b50', "zi3" },    // 子
            { '\u548c', "he2" },    // 和
            { '\u4f60', "ni3" },    // 你
            { '\u5730', "di4" },    // 地
            { '\u51fa', "chu1" },   // 出
            { '\u9053', "dao4" },   // 道
            { '\u4e5f', "ye3" },    // 也
            { '\u65f6', "shi2" },   // 时
            { '\u5e74', "nian2" },  // 年
            { '\u5f97', "de2" },    // 得
            { '\u5c31', "jiu4" },   // 就
            { '\u90a3', "na4" },    // 那
            { '\u8981', "yao4" },   // 要
            { '\u4e0b', "xia4" },   // 下
            { '\u4ee5', "yi3" },    // 以
            { '\u751f', "sheng1" }, // 生
            { '\u4f1a', "hui4" },   // 会
            { '\u81ea', "zi4" },    // 自
            { '\u7740', "zhe5" },   // 着
            { '\u53bb', "qu4" },    // 去
            { '\u4e4b', "zhi1" },   // 之
            { '\u8fc7', "guo4" },   // 过
            { '\u5bb6', "jia1" },   // 家
            { '\u5b66', "xue2" },   // 学
            { '\u5bf9', "dui4" },   // 对
            { '\u53ef', "ke3" },    // 可
            { '\u5979', "ta1" },    // 她
            { '\u91cc', "li3" },    // 里
            { '\u540e', "hou4" },   // 后
            { '\u5c0f', "xiao3" },  // 小
            { '\u4e48', "me5" },    // 么
            { '\u5fc3', "xin1" },   // 心
            { '\u591a', "duo1" },   // 多
            { '\u5929', "tian1" },  // 天
            { '\u800c', "er2" },    // 而
            { '\u80fd', "neng2" },  // 能
            { '\u597d', "hao3" },   // 好
            { '\u90fd', "dou1" },   // 都
            { '\u7136', "ran2" },   // 然
            { '\u6ca1', "mei2" },   // 没
            { '\u65e5', "ri4" },    // 日
            { '\u4e8e', "yu2" },    // 于
            { '\u8d77', "qi3" },    // 起
            { '\u8fd8', "hai2" },   // 还
            { '\u53d1', "fa1" },    // 发
            { '\u6210', "cheng2" }, // 成
            { '\u4e8b', "shi4" },   // 事
            { '\u53ea', "zhi3" },   // 只
            { '\u4f5c', "zuo4" },   // 作
            { '\u5f53', "dang1" },  // 当
            { '\u60f3', "xiang3" }, // 想
            { '\u770b', "kan4" },   // 看
            { '\u6587', "wen2" },   // 文
            { '\u65e0', "wu2" },    // 无
            { '\u5f00', "kai1" },   // 开
            { '\u624b', "shou3" },  // 手
            { '\u5341', "shi2" },   // 十
            { '\u7528', "yong4" },  // 用
            { '\u4e3b', "zhu3" },   // 主
            { '\u884c', "xing2" },  // 行
            { '\u65b9', "fang1" },  // 方
            { '\u53c8', "you4" },   // 又
            { '\u5982', "ru2" },    // 如
            { '\u524d', "qian2" },  // 前
            { '\u6240', "suo3" },   // 所
            { '\u672c', "ben3" },   // 本
            { '\u89c1', "jian4" },  // 见
            { '\u7ecf', "jing1" },  // 经
            { '\u5934', "tou2" },   // 头
            { '\u9762', "mian4" },  // 面
            { '\u8d70', "zou3" },   // 走
            { '\u5f88', "hen3" },   // 很
            { '\u957f', "chang2" }, // 长
            { '\u8ba9', "rang4" },  // 让
            { '\u4e24', "liang3" }, // 两
            { '\u6837', "yang4" },  // 样
            { '\u5176', "qi2" },    // 其
            { '\u4e49', "yi4" },    // 义
            // ---- 101-200 ----
            { '\u5df2', "yi3" },    // 已
            { '\u95ee', "wen4" },   // 问
            { '\u6700', "zui4" },   // 最
            { '\u6cd5', "fa3" },    // 法
            { '\u7b49', "deng3" },  // 等
            { '\u6b64', "ci3" },    // 此
            { '\u5b9e', "shi2" },   // 实
            { '\u4f53', "ti3" },    // 体
            { '\u4f46', "dan4" },   // 但
            { '\u4f55', "he2" },    // 何
            { '\u5c06', "jiang1" }, // 将
            { '\u5916', "wai4" },   // 外
            { '\u5df1', "ji3" },    // 己
            { '\u522b', "bie2" },   // 别
            { '\u7ed9', "gei3" },   // 给
            { '\u540d', "ming2" },  // 名
            { '\u7b2c', "di4" },    // 第
            { '\u5973', "nv3" },    // 女
            { '\u5165', "ru4" },    // 入
            { '\u4e09', "san1" },   // 三
            { '\u6b21', "ci4" },    // 次
            { '\u8001', "lao3" },   // 老
            { '\u5c11', "shao3" },  // 少
            { '\u6bcf', "mei3" },   // 每
            { '\u5de6', "zuo3" },   // 左
            { '\u53e3', "kou3" },   // 口
            { '\u5de5', "gong1" },  // 工
            { '\u5411', "xiang4" }, // 向
            { '\u4e8c', "er4" },    // 二
            { '\u5185', "nei4" },   // 内
            { '\u5e76', "bing4" },  // 并
            { '\u88ab', "bei4" },   // 被
            { '\u6574', "zheng3" }, // 整
            { '\u5173', "guan1" },  // 关
            { '\u660e', "ming2" },  // 明
            { '\u6b63', "zheng4" }, // 正
            { '\u4f7f', "shi3" },   // 使
            { '\u6b7b', "si3" },    // 死
            { '\u6c14', "qi4" },    // 气
            { '\u793e', "she4" },   // 社
            { '\u628a', "ba3" },    // 把
            { '\u58f0', "sheng1" }, // 声
            { '\u4f4d', "wei4" },   // 位
            { '\u5168', "quan2" },  // 全
            // ---- 201-300 ----
            { '\u529b', "li4" },    // 力
            { '\u60c5', "qing2" },  // 情
            { '\u5316', "hua4" },   // 化
            { '\u592a', "tai4" },   // 太
            { '\u8eab', "shen1" },  // 身
            { '\u77e5', "zhi1" },   // 知
            { '\u5c71', "shan1" },  // 山
            { '\u7236', "fu4" },    // 父
            { '\u6253', "da3" },    // 打
            { '\u7406', "li3" },    // 理
            { '\u773c', "yan3" },   // 眼
            { '\u52a8', "dong4" },  // 动
            { '\u673a', "ji1" },    // 机
            { '\u5143', "yuan2" },  // 元
            { '\u5148', "xian1" },  // 先
            { '\u56de', "hui2" },   // 回
            { '\u4e16', "shi4" },   // 世
            { '\u91cd', "zhong4" }, // 重
            { '\u56db', "si4" },    // 四
            { '\u76f8', "xiang1" }, // 相
            { '\u8bdd', "hua4" },   // 话
            { '\u65b0', "xin1" },   // 新
            { '\u611f', "gan3" },   // 感
            { '\u610f', "yi4" },    // 意
            { '\u7b11', "xiao4" },  // 笑
            { '\u679c', "guo3" },   // 果
            { '\u5b9a', "ding4" },  // 定
            { '\u8bf7', "qing3" },  // 请
            { '\u90e8', "bu4" },    // 部
            { '\u5c40', "ju2" },    // 局
            { '\u5199', "xie3" },   // 写
            { '\u52a0', "jia1" },   // 加
            { '\u5e94', "ying1" },  // 应
            { '\u91d1', "jin1" },   // 金
            { '\u6d3b', "huo2" },   // 活
            { '\u56e0', "yin1" },   // 因
            { '\u5b8c', "wan2" },   // 完
            { '\u5404', "ge4" },    // 各
            { '\u89c9', "jue2" },   // 觉
            { '\u6b65', "bu4" },    // 步
            { '\u53cd', "fan3" },   // 反
            { '\u5904', "chu4" },   // 处
            { '\u5e08', "shi1" },   // 师
            { '\u4ea7', "chan3" },   // 产
            { '\u6c11', "min2" },   // 民
            { '\u8fd0', "yun4" },   // 运
            { '\u5e02', "shi4" },   // 市
            { '\u4e1a', "ye4" },    // 业
            { '\u5236', "zhi4" },   // 制
            { '\u4e0e', "yu3" },    // 与
            // ---- 301-400 ----
            { '\u6027', "xing4" },  // 性
            { '\u5fc5', "bi4" },    // 必
            { '\u73b0', "xian4" },  // 现
            { '\u5b57', "zi4" },    // 字
            { '\u4fe1', "xin4" },   // 信
            { '\u6b22', "huan1" },  // 欢
            { '\u7ed3', "jie2" },   // 结
            { '\u5b83', "ta1" },    // 它
            { '\u4e66', "shu1" },   // 书
            { '\u6c34', "shui3" },  // 水
            { '\u89e3', "jie3" },   // 解
            { '\u5373', "ji2" },    // 即
            { '\u5efa', "jian4" },  // 建
            { '\u7cfb', "xi4" },    // 系
            { '\u5bfc', "dao3" },   // 导
            { '\u5386', "li4" },    // 历
            { '\u8fd1', "jin4" },   // 近
            { '\u8bba', "lun4" },   // 论
            { '\u8003', "kao3" },   // 考
            { '\u4efd', "fen4" },   // 份
            { '\u795e', "shen2" },  // 神
            { '\u53f2', "shi3" },   // 史
            { '\u53f0', "tai2" },   // 台
            { '\u6559', "jiao4" },  // 教
            { '\u5c3d', "jin4" },   // 尽
            { '\u6b4c', "ge1" },    // 歌
            { '\u6708', "yue4" },   // 月
            { '\u6d77', "hai3" },   // 海
            { '\u9898', "ti2" },    // 题
            { '\u66f4', "geng4" },  // 更
            { '\u8d39', "fei4" },   // 费
            { '\u653f', "zheng4" }, // 政
            { '\u6761', "tiao2" },  // 条
            { '\u8ba1', "ji4" },    // 计
            { '\u6307', "zhi3" },   // 指
            { '\u8fdb', "jin4" },   // 进
            { '\u6570', "shu4" },   // 数
            { '\u8868', "biao3" },  // 表
            { '\u5e73', "ping2" },  // 平
            { '\u6cbb', "zhi4" },   // 治
            { '\u670d', "fu2" },    // 服
            { '\u8d28', "zhi4" },   // 质
            { '\u6c42', "qiu2" },   // 求
            { '\u5317', "bei3" },   // 北
            { '\u5357', "nan2" },   // 南
            { '\u897f', "xi1" },    // 西
            { '\u4e1c', "dong1" },  // 东
            { '\u6839', "gen1" },   // 根
            { '\u5171', "gong4" },  // 共
            { '\u53ca', "ji2" },    // 及
            // ---- 401-500 ----
            { '\u8def', "lu4" },    // 路
            { '\u9ad8', "gao1" },   // 高
            { '\u62a5', "bao4" },   // 报
            { '\u6539', "gai3" },   // 改
            { '\u7fa4', "qun2" },   // 群
            { '\u62c9', "la1" },    // 拉
            { '\u8fde', "lian2" },  // 连
            { '\u671b', "wang4" },  // 望
            { '\u6e05', "qing1" },  // 清
            { '\u653e', "fang4" },  // 放
            { '\u4ea4', "jiao1" },  // 交
            { '\u4fbf', "bian4" },  // 便
            { '\u767d', "bai2" },   // 白
            { '\u5f71', "ying3" },  // 影
            { '\u6b66', "wu3" },    // 武
            { '\u8bfe', "ke4" },    // 课
            { '\u5668', "qi4" },    // 器
            { '\u7f8e', "mei3" },   // 美
            { '\u77f3', "shi2" },   // 石
            { '\u6234', "dai4" },   // 戴
            { '\u6c5f', "jiang1" }, // 江
            { '\u4e07', "wan4" },   // 万
            { '\u53e4', "gu3" },    // 古
            { '\u529e', "ban4" },   // 办
            { '\u5f3a', "qiang2" }, // 强
            { '\u4ef6', "jian4" },  // 件
            { '\u5c14', "er3" },    // 尔
            { '\u56e2', "tuan2" },  // 团
            { '\u5c3c', "ni2" },    // 尼
            { '\u4ee3', "dai4" },   // 代
            { '\u7075', "ling2" },  // 灵
            { '\u53cb', "you3" },   // 友
            { '\u5149', "guang1" }, // 光
            // ---- Daily life / common vocabulary ----
            { '\u5403', "chi1" },   // 吃
            { '\u559d', "he1" },    // 喝
            { '\u7761', "shui4" },  // 睡
            { '\u5750', "zuo4" },   // 坐
            { '\u7ad9', "zhan4" },  // 站
            { '\u8dd1', "pao3" },   // 跑
            { '\u98de', "fei1" },   // 飞
            { '\u4e70', "mai3" },   // 买
            { '\u5356', "mai4" },   // 卖
            { '\u5e2e', "bang1" },  // 帮
            { '\u5e26', "dai4" },   // 带
            { '\u62ff', "na2" },    // 拿
            { '\u542c', "ting1" },  // 听
            { '\u8bfb', "du2" },    // 读
            { '\u7b54', "da2" },    // 答
            { '\u4f4f', "zhu4" },   // 住
            { '\u54ed', "ku1" },    // 哭
            { '\u7231', "ai4" },    // 爱
            { '\u604b', "lian4" },  // 恋
            { '\u6015', "pa4" },    // 怕
            { '\u6025', "ji2" },    // 急
            { '\u5e72', "gan4" },   // 干
            { '\u8ba4', "ren4" },   // 认
            { '\u8bc6', "shi2" },   // 识
            { '\u5fd8', "wang4" },  // 忘
            { '\u8bb0', "ji4" },    // 记
            { '\u613f', "yuan4" },  // 愿
            { '\u559c', "xi3" },    // 喜
            { '\u4e50', "le4" },    // 乐
            { '\u6028', "yuan4" },  // 怨
            { '\u6068', "hen4" },   // 恨
            // ---- Nature and environment ----
            { '\u706b', "huo3" },   // 火
            { '\u571f', "tu3" },    // 土
            { '\u6728', "mu4" },    // 木
            { '\u98ce', "feng1" },  // 风
            { '\u96e8', "yu3" },    // 雨
            { '\u96ea', "xue3" },   // 雪
            { '\u4e91', "yun2" },   // 云
            { '\u82b1', "hua1" },   // 花
            { '\u8349', "cao3" },   // 草
            { '\u6811', "shu4" },   // 树
            { '\u9e1f', "niao3" },  // 鸟
            { '\u9c7c', "yu2" },    // 鱼
            { '\u9a6c', "ma3" },    // 马
            { '\u725b', "niu2" },   // 牛
            { '\u7f8a', "yang2" },  // 羊
            { '\u72d7', "gou3" },   // 狗
            { '\u732b', "mao1" },   // 猫
            { '\u9f99', "long2" },  // 龙
            { '\u864e', "hu3" },    // 虎
            { '\u6cb3', "he2" },    // 河
            { '\u6e56', "hu2" },    // 湖
            { '\u5c9b', "dao3" },   // 岛
            { '\u661f', "xing1" },  // 星
            { '\u9633', "yang2" },  // 阳
            { '\u6625', "chun1" },  // 春
            { '\u590f', "xia4" },   // 夏
            { '\u79cb', "qiu1" },   // 秋
            { '\u51ac', "dong1" },  // 冬
            // ---- Body parts ----
            { '\u8033', "er3" },    // 耳
            { '\u9f3b', "bi2" },    // 鼻
            { '\u5634', "zui3" },   // 嘴
            { '\u811a', "jiao3" },  // 脚
            { '\u8111', "nao3" },   // 脑
            { '\u8840', "xue4" },   // 血
            { '\u9aa8', "gu3" },    // 骨
            { '\u8089', "rou4" },   // 肉
            { '\u7259', "ya2" },    // 牙
            { '\u5507', "chun2" },  // 唇
            { '\u80a9', "jian1" },  // 肩
            { '\u8170', "yao1" },   // 腰
            { '\u80cc', "bei4" },   // 背
            { '\u8179', "fu4" },    // 腹
            { '\u80f8', "xiong1" }, // 胸
            // ---- Family ----
            { '\u6bcd', "mu3" },    // 母
            { '\u5144', "xiong1" }, // 兄
            { '\u5f1f', "di4" },    // 弟
            { '\u59d0', "jie3" },   // 姐
            { '\u59b9', "mei4" },   // 妹
            { '\u592b', "fu1" },    // 夫
            { '\u59bb', "qi1" },    // 妻
            { '\u513f', "er2" },    // 儿
            // ---- Numbers ----
            { '\u96f6', "ling2" },  // 零
            { '\u4e94', "wu3" },    // 五
            { '\u516d', "liu4" },   // 六
            { '\u4e03', "qi1" },    // 七
            { '\u516b', "ba1" },    // 八
            { '\u4e5d', "jiu3" },   // 九
            { '\u767e', "bai3" },   // 百
            { '\u5343', "qian1" },  // 千
            { '\u4ebf', "yi4" },    // 亿
            { '\u534a', "ban4" },   // 半
            // ---- Time ----
            { '\u5206', "fen1" },   // 分
            { '\u79d2', "miao3" },  // 秒
            { '\u665a', "wan3" },   // 晚
            { '\u65e9', "zao3" },   // 早
            { '\u4eca', "jin1" },   // 今
            { '\u6628', "zuo2" },   // 昨
            { '\u5468', "zhou1" },  // 周
            // ---- Colors ----
            { '\u7ea2', "hong2" },  // 红
            { '\u7eff', "lv4" },    // 绿
            { '\u84dd', "lan2" },   // 蓝
            { '\u9ec4', "huang2" }, // 黄
            { '\u9ed1', "hei1" },   // 黑
            { '\u7d2b', "zi3" },    // 紫
            // ---- Directions / positions ----
            { '\u53f3', "you4" },   // 右
            { '\u95f4', "jian1" },  // 间
            { '\u8fb9', "bian1" },  // 边
            { '\u5e95', "di3" },    // 底
            { '\u9876', "ding3" },  // 顶
            // ---- Government / society ----
            { '\u7701', "sheng3" }, // 省
            { '\u53bf', "xian4" },  // 县
            { '\u533a', "qu1" },    // 区
            { '\u6751', "cun1" },   // 村
            { '\u515a', "dang3" },  // 党
            { '\u519b', "jun1" },   // 军
            { '\u8b66', "jing3" },  // 警
            { '\u9662', "yuan4" },  // 院
            { '\u5e9c', "fu3" },    // 府
            { '\u6d4e', "ji4" },    // 济
            { '\u79d1', "ke1" },    // 科
            { '\u6280', "ji4" },    // 技
            // ---- Education ----
            { '\u6821', "xiao4" },  // 校
            { '\u8bd5', "shi4" },   // 试
            { '\u7b14', "bi3" },    // 笔
            { '\u7eb8', "zhi3" },   // 纸
            // ---- Food ----
            { '\u996d', "fan4" },   // 饭
            { '\u83dc', "cai4" },   // 菜
            { '\u6c64', "tang1" },  // 汤
            { '\u7c73', "mi3" },    // 米
            { '\u8336', "cha2" },   // 茶
            { '\u9152', "jiu3" },   // 酒
            { '\u7cd6', "tang2" },  // 糖
            { '\u76d0', "yan2" },   // 盐
            { '\u6cb9', "you2" },   // 油
            // ---- Buildings / places ----
            { '\u623f', "fang2" },  // 房
            { '\u697c', "lou2" },   // 楼
            { '\u5e97', "dian4" },  // 店
            { '\u5382', "chang3" }, // 厂
            { '\u8857', "jie1" },   // 街
            { '\u6865', "qiao2" },  // 桥
            { '\u95e8', "men2" },   // 门
            { '\u7a97', "chuang1" }, // 窗
            { '\u5899', "qiang2" }, // 墙
            // ---- Clothing ----
            { '\u8863', "yi1" },    // 衣
            { '\u88e4', "ku4" },    // 裤
            { '\u978b', "xie2" },   // 鞋
            { '\u5e3d', "mao4" },   // 帽
            // ---- Transportation ----
            { '\u8f66', "che1" },   // 车
            { '\u8239', "chuan2" }, // 船
            // ---- Technology ----
            { '\u7535', "dian4" },  // 电
            { '\u7f51', "wang3" },  // 网
            // ---- Common verbs ----
            { '\u505a', "zuo4" },   // 做
            { '\u4ece', "cong2" },  // 从
            { '\u4e60', "xi2" },    // 习
            { '\u53c2', "can1" },   // 参
            { '\u8be5', "gai1" },   // 该
            // ---- Adjectives ----
            { '\u5feb', "kuai4" },   // 快
            { '\u6162', "man4" },    // 慢
            { '\u65e7', "jiu4" },    // 旧
            { '\u574f', "huai4" },   // 坏
            { '\u8fdc', "yuan3" },   // 远
            { '\u77ed', "duan3" },   // 短
            { '\u80d6', "pang4" },   // 胖
            { '\u7626', "shou4" },   // 瘦
            { '\u5bcc', "fu4" },     // 富
            { '\u7a77', "qiong2" },  // 穷
            { '\u8f7b', "qing1" },   // 轻
            { '\u6697', "an4" },     // 暗
            { '\u51b7', "leng3" },   // 冷
            { '\u70ed', "re4" },     // 热
            { '\u786c', "ying4" },   // 硬
            { '\u8f6f', "ruan3" },   // 软
            { '\u6e7f', "shi1" },    // 湿
            { '\u5bbd', "kuan1" },   // 宽
            { '\u7a84', "zhai3" },   // 窄
            { '\u6df1', "shen1" },   // 深
            { '\u6d45', "qian3" },   // 浅
            { '\u9999', "xiang1" },  // 香
            { '\u81ed', "chou4" },   // 臭
            { '\u7518', "gan1" },    // 甘
            { '\u82e6', "ku3" },     // 苦
            { '\u9178', "suan1" },   // 酸
            { '\u8fa3', "la4" },     // 辣
            // ---- Function words / adverbs ----
            { '\u975e', "fei1" },    // 非
            { '\u5e38', "chang2" },  // 常
            { '\u66fe', "ceng2" },   // 曾
            { '\u521a', "gang1" },   // 刚
            { '\u624d', "cai2" },    // 才
            { '\u5374', "que4" },    // 却
            { '\u867d', "sui1" },    // 虽
            { '\u518d', "zai4" },    // 再
            { '\u4ec5', "jin3" },    // 仅
            { '\u83ab', "mo4" },     // 莫
            // ---- Countries / surnames ----
            { '\u82f1', "ying1" },   // 英
            { '\u5fb7', "de2" },     // 德
            { '\u4fc4', "e2" },      // 俄
            { '\u97e9', "han2" },    // 韩
            { '\u5370', "yin4" },    // 印
            { '\u6fb3', "ao4" },     // 澳
            { '\u6e2f', "gang3" },   // 港
            { '\u6797', "lin2" },    // 林
            { '\u5f20', "zhang1" },  // 张
            { '\u674e', "li3" },     // 李
            { '\u738b', "wang2" },   // 王
            { '\u9648', "chen2" },   // 陈
            { '\u5218', "liu2" },    // 刘
            { '\u8d75', "zhao4" },   // 赵
            { '\u5434', "wu2" },     // 吴
            { '\u5b59', "sun1" },    // 孙
            { '\u6731', "zhu1" },    // 朱
            { '\u80e1', "hu2" },     // 胡
            { '\u6881', "liang2" },  // 梁
            { '\u5b8b', "song4" },   // 宋
            { '\u5510', "tang2" },   // 唐
            { '\u8bb8', "xu3" },     // 许
            { '\u4e01', "ding1" },   // 丁
            { '\u90d1', "zheng4" },  // 郑
            // ---- Emotions / mental states ----
            { '\u601d', "si1" },     // 思
            { '\u5ff5', "nian4" },   // 念
            { '\u5b89', "an1" },     // 安
            { '\u5371', "wei1" },    // 危
            { '\u4eb2', "qin1" },    // 亲
            { '\u51b3', "jue2" },    // 决
            { '\u7b56', "ce4" },     // 策
            { '\u7a0b', "cheng2" },  // 程
            // ---- Medicine / health ----
            { '\u533b', "yi1" },     // 医
            { '\u836f', "yao4" },    // 药
            { '\u75c5', "bing4" },   // 病
            { '\u75db', "tong4" },   // 痛
            // ---- Abstract concepts ----
            { '\u5584', "shan4" },   // 善
            { '\u771f', "zhen1" },   // 真
            { '\u5047', "jia3" },    // 假
            { '\u865a', "xu1" },     // 虚
            { '\u590d', "fu4" },     // 复
            { '\u6742', "za2" },     // 杂
            { '\u7b80', "jian3" },   // 简
            { '\u5355', "dan1" },    // 单
            { '\u96be', "nan2" },    // 难
            { '\u6613', "yi4" },     // 易
            { '\u5e78', "xing4" },   // 幸
            { '\u798f', "fu2" },     // 福
            { '\u5bff', "shou4" },   // 寿
            { '\u8bda', "cheng2" },  // 诚
            { '\u5b88', "shou3" },   // 守
            { '\u4fdd', "bao3" },    // 保
            { '\u62a4', "hu4" },     // 护
            { '\u6551', "jiu4" },    // 救
            { '\u52a9', "zhu4" },    // 助
            { '\u8d5e', "zan4" },    // 赞
            // ---- Additional essential characters ----
            { '\u5e1d', "di4" },     // 帝
            { '\u7687', "huang2" },  // 皇
            { '\u4eac', "jing1" },   // 京
            { '\u6d32', "zhou1" },   // 洲
            { '\u6b27', "ou1" },     // 欧
            { '\u6d3e', "pai4" },    // 派
            { '\u80dc', "sheng4" },  // 胜
            { '\u8d25', "bai4" },    // 败
            { '\u6218', "zhan4" },   // 战
            { '\u7ea7', "ji2" },     // 级
            { '\u6548', "xiao4" },   // 效
            { '\u7ba1', "guan3" },   // 管
            { '\u6784', "gou4" },    // 构
            { '\u7ec4', "zu3" },     // 组
            { '\u7ec7', "zhi1" },    // 织
            { '\u7269', "wu4" },     // 物
            { '\u690d', "zhi2" },    // 植
            { '\u672f', "shu4" },    // 术
            { '\u519c', "nong2" },   // 农
            { '\u5546', "shang1" },  // 商
            { '\u52a1', "wu4" },     // 务
            { '\u961f', "dui4" },    // 队
            { '\u65cf', "zu2" },     // 族
            { '\u5177', "ju4" },     // 具
            { '\u6750', "cai2" },    // 材
            { '\u6599', "liao4" },   // 料
            { '\u54c1', "pin3" },    // 品
            { '\u8d27', "huo4" },    // 货
            { '\u94b1', "qian2" },   // 钱
            { '\u8d35', "gui4" },    // 贵
            { '\u5b9c', "yi2" },     // 宜
            { '\u5229', "li4" },     // 利
            { '\u76ca', "yi4" },     // 益
            { '\u606f', "xi1" },     // 息
            { '\u4ef7', "jia4" },    // 价
            { '\u503c', "zhi2" },    // 值
            // ---- More common verbs ----
            { '\u95fb', "wen2" },    // 闻
            { '\u63a8', "tui1" },    // 推
            { '\u62b1', "bao4" },    // 抱
            { '\u6254', "reng1" },   // 扔
            { '\u63a5', "jie1" },    // 接
            { '\u6536', "shou1" },   // 收
            { '\u9001', "song4" },   // 送
            { '\u843d', "luo4" },    // 落
            { '\u5347', "sheng1" },  // 升
            { '\u964d', "jiang4" },  // 降
            { '\u6d88', "xiao1" },   // 消
            { '\u5931', "shi1" },    // 失
            { '\u8d62', "ying2" },   // 赢
            { '\u8f93', "shu1" },    // 输
            { '\u641c', "sou1" },    // 搜
            { '\u7d22', "suo3" },    // 索
            { '\u8f7d', "zai4" },    // 载
            { '\u88c5', "zhuang1" }, // 装
            { '\u5220', "shan1" },   // 删
            { '\u9664', "chu2" },    // 除
            { '\u5b58', "cun2" },    // 存
            { '\u50a8', "chu3" },    // 储
            { '\u5907', "bei4" },    // 备
            { '\u5bb9', "rong2" },   // 容
            { '\u5141', "yun3" },    // 允
            { '\u8bbe', "she4" },    // 设
            { '\u5212', "hua4" },    // 划
            { '\u7248', "ban3" },    // 版
            { '\u7801', "ma3" },     // 码
            { '\u6ce8', "zhu4" },    // 注
            { '\u518c', "ce4" },     // 册
            { '\u767b', "deng1" },   // 登
            { '\u5f55', "lu4" },     // 录
            { '\u9000', "tui4" },    // 退
            // ---- More characters ----
            { '\u5e03', "bu4" },     // 布
            { '\u679d', "zhi1" },    // 枝
            { '\u53f6', "ye4" },     // 叶
            { '\u5a92', "mei2" },    // 媒
            { '\u64ad', "bo1" },     // 播
            { '\u8282', "jie2" },    // 节
            { '\u76ee', "mu4" },     // 目
            { '\u5c55', "zhan3" },   // 展
            { '\u59cb', "shi3" },    // 始
            { '\u7ee7', "ji4" },     // 继
            { '\u7eed', "xu4" },     // 续
            { '\u505c', "ting2" },   // 停
            { '\u6b62', "zhi3" },    // 止
            { '\u5747', "jun1" },    // 均
            { '\u675f', "shu4" },    // 束
            { '\u7bc7', "pian1" },   // 篇
            { '\u5377', "juan4" },   // 卷
            { '\u5e10', "zhang4" },  // 帐
            { '\u6237', "hu4" },     // 户
            { '\u5ba2', "ke4" },     // 客
            { '\u670b', "peng2" },   // 朋
            { '\u4ed9', "xian1" },   // 仙
            { '\u5723', "sheng4" },  // 圣
            { '\u4f5b', "fo2" },     // 佛
            { '\u9b3c', "gui3" },    // 鬼
            { '\u9b54', "mo2" },     // 魔
            { '\u7cbe', "jing1" },   // 精
            { '\u602a', "guai4" },   // 怪
            // ---- Measure words ----
            { '\u5757', "kuai4" },   // 块
            { '\u5c01', "feng1" },   // 封
            { '\u5c42', "ceng2" },   // 层
            { '\u7c92', "li4" },     // 粒
            { '\u53cc', "shuang1" }, // 双
            { '\u5957', "tao4" },    // 套
            { '\u8f86', "liang4" },  // 辆
            { '\u8258', "sou1" },    // 艘
            { '\u67b6', "jia4" },    // 架
            { '\u5ea7', "zuo4" },    // 座
            // ---- Additional frequently used ----
            { '\u62c5', "dan1" },    // 担
            { '\u89c2', "guan1" },   // 观
            { '\u5c5e', "shu3" },    // 属
            { '\u6295', "tou2" },    // 投
            { '\u8d44', "zi1" },     // 资
            { '\u6e90', "yuan2" },   // 源
            { '\u7edf', "tong3" },   // 统
            { '\u5f8b', "lv4" },     // 律
            { '\u89c4', "gui1" },    // 规
            { '\u8303', "fan4" },    // 范
            { '\u57fa', "ji1" },     // 基
            { '\u7840', "chu3" },    // 础
            { '\u7814', "yan2" },    // 研
            { '\u7a76', "jiu1" },    // 究
            { '\u53f7', "hao4" },    // 号
            { '\u91cf', "liang4" },  // 量
            { '\u5ea6', "du4" },     // 度
            { '\u53e5', "ju4" },     // 句
            { '\u89d2', "jiao3" },   // 角
            { '\u5706', "yuan2" },   // 圆
            { '\u7ebf', "xian4" },   // 线
            { '\u5f62', "xing2" },   // 形
            { '\u8272', "se4" },     // 色
            { '\u56fe', "tu2" },     // 图
            { '\u753b', "hua4" },    // 画
            { '\u821e', "wu3" },     // 舞
            { '\u97f3', "yin1" },    // 音
            { '\u8bed', "yu3" },     // 语
            { '\u8bcd', "ci2" },     // 词
            { '\u6bb5', "duan4" },   // 段
            { '\u7ae0', "zhang1" },  // 章
            { '\u9875', "ye4" },     // 页
            { '\u5e7f', "guang3" },  // 广
            { '\u544a', "gao4" },    // 告
            { '\u7167', "zhao4" },   // 照
            { '\u7247', "pian4" },   // 片
            { '\u5f0f', "shi4" },    // 式
            { '\u7c7b', "lei4" },    // 类
            { '\u6bd4', "bi3" },     // 比
            { '\u8f83', "jiao4" },   // 较
            { '\u82e5', "ruo4" },    // 若
            { '\u5219', "ze2" },     // 则
            { '\u786e', "que4" },    // 确
            { '\u73af', "huan2" },   // 环
            { '\u5883', "jing4" },   // 境
            { '\u89c6', "shi4" },    // 视
            { '\u8005', "zhe3" },    // 者
            { '\u5458', "yuan2" },   // 员
            { '\u68a6', "meng4" },   // 梦
            { '\u62e5', "yong1" },   // 拥
            { '\u6d4f', "liu2" },    // 浏
            { '\u89c8', "lan3" },    // 览
            { '\u5c1a', "shang4" },  // 尚
            { '\u8bef', "wu4" },     // 误
            { '\u6848', "an4" },     // 案
            { '\u5e0c', "xi1" },     // 希
        };
    }
}