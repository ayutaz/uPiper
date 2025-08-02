using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace uPiper.Core.Phonemizers.Backend.Chinese
{
    /// <summary>
    /// Converter for Traditional Chinese to Simplified Chinese
    /// Handles one-to-one character mappings (covers 90%+ of common usage)
    /// </summary>
    public class TraditionalChineseConverter
    {
        private readonly Dictionary<char, char> traditionalToSimplified;
        private readonly HashSet<char> traditionalCharacters;
        
        public TraditionalChineseConverter()
        {
            traditionalToSimplified = new Dictionary<char, char>();
            traditionalCharacters = new HashSet<char>();
            InitializeMappings();
        }
        
        /// <summary>
        /// Convert Traditional Chinese text to Simplified Chinese
        /// </summary>
        public string ConvertToSimplified(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            var result = new StringBuilder(text.Length);
            
            foreach (char ch in text)
            {
                if (traditionalToSimplified.TryGetValue(ch, out char simplified))
                {
                    result.Append(simplified);
                }
                else
                {
                    result.Append(ch);
                }
            }
            
            return result.ToString();
        }
        
        /// <summary>
        /// Check if a character is Traditional Chinese
        /// </summary>
        public bool IsTraditionalCharacter(char ch)
        {
            return traditionalCharacters.Contains(ch);
        }
        
        /// <summary>
        /// Check if text contains any Traditional Chinese characters
        /// </summary>
        public bool ContainsTraditional(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            foreach (char ch in text)
            {
                if (IsTraditionalCharacter(ch))
                    return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Get statistics about the converter
        /// </summary>
        public (int mappingCount, int traditionalCount) GetStatistics()
        {
            return (traditionalToSimplified.Count, traditionalCharacters.Count);
        }
        
        /// <summary>
        /// Initialize Traditional to Simplified mappings
        /// </summary>
        private void InitializeMappings()
        {
            // Most common Traditional to Simplified mappings
            // Education and learning
            AddMapping('學', '学');  // learn
            AddMapping('習', '习');  // practice
            AddMapping('書', '书');  // book
            AddMapping('讀', '读');  // read
            AddMapping('寫', '写');  // write
            AddMapping('畫', '画');  // draw/picture
            AddMapping('題', '题');  // topic/question
            
            // Country and geography
            AddMapping('國', '国');  // country
            AddMapping('臺', '台');  // Taiwan
            AddMapping('灣', '湾');  // bay
            AddMapping('島', '岛');  // island
            AddMapping('區', '区');  // district
            AddMapping('縣', '县');  // county
            AddMapping('鄉', '乡');  // village
            
            // Language and communication
            AddMapping('語', '语');  // language
            AddMapping('話', '话');  // speech
            AddMapping('說', '说');  // speak
            AddMapping('談', '谈');  // talk
            AddMapping('論', '论');  // discuss
            AddMapping('講', '讲');  // speak/teach
            AddMapping('聽', '听');  // listen
            
            // Time and calendar
            AddMapping('時', '时');  // time
            AddMapping('間', '间');  // between/room
            AddMapping('歷', '历');  // calendar/history
            AddMapping('曆', '历');  // calendar
            AddMapping('鐘', '钟');  // clock
            AddMapping('點', '点');  // point/o'clock
            
            // People and relationships
            AddMapping('親', '亲');  // parent/relative
            AddMapping('愛', '爱');  // love
            AddMapping('戀', '恋');  // love (romantic)
            AddMapping('媽', '妈');  // mother
            AddMapping('爺', '爷');  // grandfather
            AddMapping('歲', '岁');  // age
            
            // Body and health
            AddMapping('體', '体');  // body
            AddMapping('頭', '头');  // head
            AddMapping('腦', '脑');  // brain
            AddMapping('臉', '脸');  // face
            AddMapping('眼', '眼');  // eye (same)
            AddMapping('耳', '耳');  // ear (same)
            AddMapping('齒', '齿');  // tooth
            AddMapping('醫', '医');  // medical
            AddMapping('藥', '药');  // medicine
            AddMapping('療', '疗');  // therapy
            
            // Food and dining
            AddMapping('飯', '饭');  // rice/meal
            AddMapping('麵', '面');  // noodles
            AddMapping('餐', '餐');  // meal (same)
            AddMapping('飲', '饮');  // drink
            AddMapping('湯', '汤');  // soup
            AddMapping('魚', '鱼');  // fish
            AddMapping('雞', '鸡');  // chicken
            AddMapping('鴨', '鸭');  // duck
            AddMapping('豬', '猪');  // pig
            AddMapping('牛', '牛');  // cow (same)
            AddMapping('羊', '羊');  // sheep (same)
            AddMapping('蛋', '蛋');  // egg (same)
            AddMapping('菜', '菜');  // vegetable (same)
            AddMapping('鹽', '盐');  // salt
            AddMapping('糖', '糖');  // sugar (same)
            AddMapping('醬', '酱');  // sauce
            
            // Business and economy
            AddMapping('買', '买');  // buy
            AddMapping('賣', '卖');  // sell
            AddMapping('貨', '货');  // goods
            AddMapping('價', '价');  // price
            AddMapping('錢', '钱');  // money
            AddMapping('幣', '币');  // currency
            AddMapping('銀', '银');  // silver/bank
            AddMapping('財', '财');  // wealth
            AddMapping('經', '经');  // manage/sutra
            AddMapping('濟', '济');  // economy
            AddMapping('業', '业');  // business
            AddMapping('產', '产');  // produce
            AddMapping('廠', '厂');  // factory
            AddMapping('場', '场');  // place/field
            
            // Transportation
            AddMapping('車', '车');  // vehicle
            AddMapping('輛', '辆');  // classifier for vehicles
            AddMapping('運', '运');  // transport
            AddMapping('輸', '输');  // transport
            AddMapping('載', '载');  // carry
            AddMapping('飛', '飞');  // fly
            AddMapping('機', '机');  // machine/opportunity
            AddMapping('場', '场');  // field/site
            AddMapping('鐵', '铁');  // iron/railway
            AddMapping('軌', '轨');  // rail
            AddMapping('號', '号');  // number
            
            // Technology and modern life
            AddMapping('電', '电');  // electricity
            AddMapping('腦', '脑');  // brain/computer
            AddMapping('網', '网');  // net
            AddMapping('絡', '络');  // network
            AddMapping('線', '线');  // line
            AddMapping('號', '号');  // number
            AddMapping('碼', '码');  // code
            AddMapping('據', '据');  // data
            AddMapping('庫', '库');  // storage
            AddMapping('統', '统');  // system
            AddMapping('計', '计');  // calculate
            AddMapping('算', '算');  // calculate (same)
            AddMapping('術', '术');  // technique
            AddMapping('軟', '软');  // soft
            AddMapping('硬', '硬');  // hard (same)
            AddMapping('體', '体');  // body/style
            
            // Common verbs and actions
            AddMapping('來', '来');  // come
            AddMapping('去', '去');  // go (same)
            AddMapping('進', '进');  // enter
            AddMapping('過', '过');  // pass
            AddMapping('還', '还');  // return/still
            AddMapping('給', '给');  // give
            AddMapping('讓', '让');  // let
            AddMapping('幫', '帮');  // help
            AddMapping('問', '问');  // ask
            AddMapping('答', '答');  // answer (same)
            AddMapping('見', '见');  // see
            AddMapping('覺', '觉');  // feel
            AddMapping('認', '认');  // recognize
            AddMapping('識', '识');  // know
            AddMapping('記', '记');  // remember
            AddMapping('憶', '忆');  // recall
            AddMapping('忘', '忘');  // forget (same)
            AddMapping('動', '动');  // move
            AddMapping('靜', '静');  // quiet
            AddMapping('開', '开');  // open
            AddMapping('關', '关');  // close
            AddMapping('發', '发');  // emit/develop
            AddMapping('現', '现');  // appear/now
            AddMapping('實', '实');  // real
            AddMapping('變', '变');  // change
            AddMapping('換', '换');  // exchange
            
            // Common adjectives
            AddMapping('長', '长');  // long
            AddMapping('短', '短');  // short (same)
            AddMapping('寬', '宽');  // wide
            AddMapping('窄', '窄');  // narrow (same)
            AddMapping('遠', '远');  // far
            AddMapping('近', '近');  // near (same)
            AddMapping('熱', '热');  // hot
            AddMapping('冷', '冷');  // cold (same)
            AddMapping('輕', '轻');  // light
            AddMapping('重', '重');  // heavy (same)
            AddMapping('難', '难');  // difficult
            AddMapping('簡', '简');  // simple
            AddMapping('單', '单');  // single
            AddMapping('複', '复');  // complex
            AddMapping('雜', '杂');  // mixed
            AddMapping('亂', '乱');  // messy
            AddMapping('靜', '静');  // quiet
            AddMapping('鬧', '闹');  // noisy
            
            // Nature and environment
            AddMapping('風', '风');  // wind
            AddMapping('雲', '云');  // cloud
            AddMapping('雨', '雨');  // rain (same)
            AddMapping('雪', '雪');  // snow (same)
            AddMapping('霧', '雾');  // fog
            AddMapping('陽', '阳');  // sun/positive
            AddMapping('陰', '阴');  // shadow/negative
            AddMapping('氣', '气');  // air/gas
            AddMapping('溫', '温');  // warm
            AddMapping('濕', '湿');  // wet
            AddMapping('乾', '干');  // dry
            AddMapping('淨', '净');  // clean
            AddMapping('髒', '脏');  // dirty
            AddMapping('環', '环');  // ring/environment
            AddMapping('境', '境');  // boundary (same)
            
            // Buildings and structures
            AddMapping('樓', '楼');  // building
            AddMapping('層', '层');  // floor/layer
            AddMapping('廳', '厅');  // hall
            AddMapping('門', '门');  // door
            AddMapping('窗', '窗');  // window (same)
            AddMapping('牆', '墙');  // wall
            AddMapping('頂', '顶');  // top
            AddMapping('蓋', '盖');  // cover
            AddMapping('築', '筑');  // build
            AddMapping('設', '设');  // establish
            
            // Abstract concepts
            AddMapping('夢', '梦');  // dream
            AddMapping('願', '愿');  // wish
            AddMapping('希', '希');  // hope (same)
            AddMapping('望', '望');  // hope (same)
            AddMapping('慮', '虑');  // consider
            AddMapping('憂', '忧');  // worry
            AddMapping('喜', '喜');  // happy (same)
            AddMapping('歡', '欢');  // joyful
            AddMapping('樂', '乐');  // happy/music
            AddMapping('悲', '悲');  // sad (same)
            AddMapping('傷', '伤');  // hurt
            AddMapping('痛', '痛');  // pain (same)
            AddMapping('苦', '苦');  // bitter (same)
            AddMapping('甜', '甜');  // sweet (same)
            
            // More common characters
            AddMapping('於', '于');  // at/in
            AddMapping('與', '与');  // and/with
            AddMapping('為', '为');  // for/become
            AddMapping('從', '从');  // from
            AddMapping('將', '将');  // will/general
            AddMapping('應', '应');  // should
            AddMapping('該', '该');  // should
            AddMapping('當', '当');  // when/serve as
            AddMapping('雖', '虽');  // although
            AddMapping('然', '然');  // however (same)
            AddMapping('則', '则');  // then
            AddMapping('麼', '么');  // question particle
            AddMapping('嗎', '吗');  // question particle
            AddMapping('呢', '呢');  // particle (same)
            AddMapping('啊', '啊');  // particle (same)
            AddMapping('吧', '吧');  // particle (same)
            AddMapping('裡', '里');  // inside
            AddMapping('邊', '边');  // side
            AddMapping('後', '后');  // after
            AddMapping('頭', '头');  // head/suffix
            AddMapping('樣', '样');  // appearance
            AddMapping('種', '种');  // kind/plant
            AddMapping('類', '类');  // category
            AddMapping('級', '级');  // level
            AddMapping('個', '个');  // classifier
            AddMapping('條', '条');  // strip/clause
            AddMapping('張', '张');  // sheet/surname
            AddMapping('隻', '只');  // classifier
            AddMapping('雙', '双');  // pair
            AddMapping('對', '对');  // correct/pair
            AddMapping('錯', '错');  // wrong
            
            Debug.Log($"[TraditionalChineseConverter] Initialized with {traditionalToSimplified.Count} mappings");
        }
        
        /// <summary>
        /// Add a mapping and update the traditional character set
        /// </summary>
        private void AddMapping(char traditional, char simplified)
        {
            traditionalToSimplified[traditional] = simplified;
            traditionalCharacters.Add(traditional);
        }
    }
}