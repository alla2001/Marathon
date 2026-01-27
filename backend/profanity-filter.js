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

// Franco-Arab (Arabic profanity written in Latin/transliterated)
const FRANCO_ARAB_WORDS = [
  // Kos / Kuss variants
  'kos', 'koss', 'kus', 'kuss', 'kosomak', 'kos omak', 'kos ommak',
  'kus omak', 'kus ommak', 'kosomk', 'kos okhtak', 'kos okhtk',
  'kus ukhtak', 'kusomak',
  // Sharmouta variants
  'sharmouta', 'sharmou6a', 'sharmuta', 'sharmoota', 'sharmonta',
  'charmou6a', 'charmuta', 'charmouta',
  // A7a / Ayre variants
  'a7a', 'ah7a', 'ayre', 'ayri', 'ayree', 'airi', 'airee', 'eyre', 'eyri',
  'ayr', 'air',
  // Zeb / Zob variants
  'zeb', 'zob', 'zebi', 'zobi', 'zb', 'zeby', 'zoby',
  // Teez variants
  'teez', 'tiz', 'tez', '6eez', '6iz', 'teeze',
  // Nik / Neek variants
  'nik', 'neek', 'naik', 'nayek', 'niik', 'neik', 'n1k',
  'nikak', 'nikomak',
  // Khawal / Gay slurs
  'khawal', '5awal', 'khwal', '5wal', 'khanee8', 'khanees', 'khanith',
  // Kalb / animal insults
  'ibn elkalb', 'ibn il kalb', 'ibn alkalb', 'ya kalb', 'ya 7mar',
  'ya 7ayawan', 'ya 5anzeir', 'ya 5anzeer', 'ibn el sharmouta',
  'ibn il sharmuta',
  // Wiskh / dirty
  'ya wiskh', 'ya wisikh', 'weskh', 'wisikh',
  // M words
  'manyak', 'manyok', 'manyook', 'manyo2', 'manyake',
  'metnak', 'mitnak', 'metnaak', 'mitnaaak',
  '3ars', '3ers', 'mo3ras', 'mo3res', 'm3rs',
  // Khara variants
  'khara', '5ara', 'khra', '5ra', 'khary',
  // Ga7ba / Q7ba
  'ga7ba', 'qa7ba', 'qahba', 'gahba', 'kahba', 'ka7ba',
  // Dayouth
  'dayouth', 'dayoos', 'dayyoos', 'dayoo8', 'dayo8',
  // General
  'ibn haram', 'ibn el haram', 'ibn alharam',
  'ya 3ar', 'ya shar', 'ya zbalah',
  'laa3', 'la3an', 'yel3an',
  'ta7an', 'metnaka', 'mitnaka',
  'rooh entak', 'roo7 intak', 'roo7 entak',
  // Common short franco
  'aks', 'ya ars', 'ya 3rs',
];

// Build lookup sets for fast matching
const englishSet = new Set(ENGLISH_WORDS.map(w => w.toLowerCase()));
const arabicSet = new Set(ARABIC_WORDS);
const francoSet = new Set(FRANCO_ARAB_WORDS.map(w => w.toLowerCase()));

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

  // Check Franco-Arab (Latin transliteration) - substring match
  // Also normalize 7→h, 3→a, 5→kh, 6→t, 8→q, 2→a for franco number substitutions
  const francoNormalized = lower
    .replace(/7/g, 'h')
    .replace(/3/g, 'a')
    .replace(/5/g, 'kh')
    .replace(/6/g, 't')
    .replace(/8/g, 'q')
    .replace(/2/g, 'a');

  for (const word of francoSet) {
    if (word.length >= 3 && (lower.includes(word) || francoNormalized.includes(word))) {
      return { isProfane: true, reason: 'inappropriate language detected' };
    }
    if (word.length < 3 && (lower === word || francoNormalized === word)) {
      return { isProfane: true, reason: 'inappropriate language detected' };
    }
  }

  // Check Arabic script - substring match
  const trimmed = username.trim();
  for (const word of arabicSet) {
    if (trimmed.includes(word)) {
      return { isProfane: true, reason: 'inappropriate language detected' };
    }
  }

  return { isProfane: false, reason: '' };
}

module.exports = { checkProfanity };
