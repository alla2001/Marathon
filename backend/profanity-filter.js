// ============================================
// Profanity Filter - English & Arabic
// ============================================
// Used by both server.js and fm-server.js
// Returns true if username contains profanity

const ENGLISH_WORDS = [
  // Common English profanity
  'fuck', 'shit', 'ass', 'asshole', 'bitch', 'bastard', 'damn', 'dick',
  'pussy', 'cock', 'cunt', 'whore', 'slut', 'fag', 'faggot', 'nigger',
  'nigga', 'retard', 'piss', 'crap', 'bollocks', 'wanker', 'twat',
  'prick', 'douche', 'jackass', 'motherfucker', 'bullshit', 'shithead',
  'dumbass', 'dipshit', 'goddamn', 'arsehole', 'arse', 'tosser',
  'bellend', 'knob', 'minge', 'spastic', 'homo', 'dyke', 'tranny',
  'skank', 'hoe', 'boob', 'tit', 'tits', 'penis', 'vagina', 'dildo',
  'jizz', 'cum', 'semen', 'erection', 'orgasm', 'blowjob', 'handjob',
  'anal', 'porn', 'sexy', 'nude', 'naked', 'sex', 'rape', 'rapist',
  'molest', 'pedophile', 'paedo', 'nazi', 'hitler', 'kkk', 'jihad',
  'terrorist', 'kill', 'murder', 'suicide',
  // Leet speak / evasion variants
  'f0ck', 'fuk', 'fuq', 'fck', 'sht', 'sh1t', 'b1tch', 'btch',
  'd1ck', 'p0rn', 'pr0n', 'a55', 'azz', 'phuk', 'phuck',
  'n1gger', 'n1gga', 'nigg', 'f4g', 'c0ck', 'kunt',
];

const ARABIC_WORDS = [
  // Common Arabic profanity (multiple dialects)
  'كس', 'طيز', 'زب', 'شرموطة', 'عرص', 'متناك', 'منيوك',
  'قحبة', 'لبوة', 'كلب', 'حمار', 'خنزير', 'ابن الكلب',
  'يلعن', 'لعنة', 'زنا', 'فاجرة', 'داعرة', 'عاهرة',
  'خول', 'مخنث', 'لوطي', 'ديوث', 'قواد', 'نيك',
  'احا', 'اير', 'زبي', 'كسمك', 'كس امك', 'كس اختك',
  'ابن القحبة', 'ابن الشرموطة', 'ولد الكلب', 'يا حيوان',
  'خرا', 'زق', 'بعبوص', 'عير', 'منيوكة', 'متناكة',
  'شرموط', 'معرص', 'مقعد', 'واطي', 'حقير', 'نجس',
  'يا وسخ', 'وسخة', 'زبالة', 'حثالة', 'تفو', 'انقلع',
  'كسختك', 'طيزك', 'زبك', 'نياكة', 'نيكة',
  'ابن الحرام', 'بنت الحرام', 'حرامي',
  // Gulf dialect
  'ثور', 'جحش', 'تيس', 'غبي', 'معفن',
  // Egyptian dialect
  'وسخ', 'ابن المتناكة', 'يا ابن اللبوة', 'شرموطة امك',
  // Levantine
  'كس اخت', 'روح انتاك', 'لك كسك',
];

// Build lookup sets for fast matching
const englishSet = new Set(ENGLISH_WORDS.map(w => w.toLowerCase()));
const arabicSet = new Set(ARABIC_WORDS);

/**
 * Check if a username contains profanity.
 * Uses substring matching to catch embedded profanity (e.g. "xfuckx").
 * @param {string} username
 * @returns {{ isProfane: boolean, reason: string }}
 */
function checkProfanity(username) {
  if (!username || typeof username !== 'string') {
    return { isProfane: false, reason: '' };
  }

  const lower = username.toLowerCase().trim();
  // Remove common substitutions for English check
  const normalized = lower
    .replace(/0/g, 'o')
    .replace(/1/g, 'i')
    .replace(/3/g, 'e')
    .replace(/4/g, 'a')
    .replace(/5/g, 's')
    .replace(/7/g, 't')
    .replace(/@/g, 'a')
    .replace(/\$/g, 's')
    .replace(/!/g, 'i')
    .replace(/\+/g, 't');

  // Check English - substring match
  for (const word of englishSet) {
    if (word.length >= 3 && (lower.includes(word) || normalized.includes(word))) {
      return { isProfane: true, reason: 'inappropriate language detected' };
    }
    // Exact match for short words (2 chars)
    if (word.length < 3 && (lower === word || normalized === word)) {
      return { isProfane: true, reason: 'inappropriate language detected' };
    }
  }

  // Check Arabic - substring match
  const trimmed = username.trim();
  for (const word of arabicSet) {
    if (trimmed.includes(word)) {
      return { isProfane: true, reason: 'inappropriate language detected' };
    }
  }

  return { isProfane: false, reason: '' };
}

module.exports = { checkProfanity };
