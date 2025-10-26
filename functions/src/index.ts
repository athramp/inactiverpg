import * as admin from "firebase-admin";
import {onCall, HttpsError} from "firebase-functions/v2/https";

admin.initializeApp();
const db = admin.firestore();

type Stats = {
  hp:number;
  atk:number;
  def:number;
  spd:number;
  critChance:number;
  critMult:number;
};

type Profile = {
  uid:string;
  name:string;
  class:string;
  level:number;
  powerScore:number;
  lastActive:number;
  stats:Stats;
  buildHash:string;
};

type Fighter = Stats&{id:string};

type Turn = {
  a:string; // attacker uid
  dmg:number; // damage
  c:boolean; // crit
  hp:number; // defender HP after
};

/**
 * Simulates a deterministic duel between two player profiles.
 * @param {Profile} a - The attacker’s public profile.
 * @param {Profile} d - The defender’s public profile.
 * @param {number} seed - Random seed for deterministic RNG.
 * @return {{winnerId: string, turns: Turn[]}} The duel outcome and turn log.
 */
function resolve(
  a:Profile,
  d:Profile,
  seed:number
):{winnerId:string;turns:Turn[]} {
  let state = seed>>>0;
  const rnd = ():number=>{
    state = (1103515245*state+12345)>>>0;
    return (state&0x7fffffff)/0x80000000;
  };

  const A:Fighter = {...a.stats, id: a.uid};
  const D:Fighter = {...d.stats, id: d.uid};

  let atk:Fighter = A.spd>=D.spd?A:D;
  let def:Fighter = atk===A?D:A;

  const turns:Turn[] = [];
  let safety = 0;

  while (A.hp>0&&D.hp>0&&safety++<200) {
    const crit = rnd()<atk.critChance;
    const base = Math.max(1, atk.atk-def.def);
    const mult = crit?atk.critMult:1;
    const dmg = Math.max(1, Math.floor(base*mult));

    def.hp = Math.max(0, def.hp-dmg);
    turns.push({a: atk.id, dmg: dmg, c: crit, hp: def.hp});

    const tmp = atk;
    atk = def;
    def = tmp;
  }

  const winnerId = A.hp>0?A.id:D.id;
  return {winnerId: winnerId, turns: turns};
}

export const startDuel = onCall(
  {enforceAppCheck: false},
  async (req)=>{
    const attackerId = req.auth?.uid;
    const defenderId = (req.data?.defenderId as string)||"";

    if (!attackerId) {
      throw new HttpsError("unauthenticated", "Login required");
    }
    if (!defenderId||defenderId===attackerId) {
      throw new HttpsError("invalid-argument", "Bad defenderId");
    }

    // throttle: 1 duel / 5s
    const throttleRef = db.doc(`users/${attackerId}/meta/throttle`);
    const throttleSnap = await throttleRef.get();
    const now = Date.now();
    const last = throttleSnap.exists ?
      Number(throttleSnap.data()?.lastStart||0) :
      0;

    if (now-last<5000) {
      throw new HttpsError("resource-exhausted", "Slow down");
    }
    await throttleRef.set({lastStart: now}, {merge: true});

    const aRef = db.collection("publicProfiles").doc(attackerId);
    const dRef = db.collection("publicProfiles").doc(defenderId);
    const [aSnap, dSnap] = await Promise.all([aRef.get(), dRef.get()]);

    if (!aSnap.exists||!dSnap.exists) {
      throw new HttpsError("not-found", "Profile missing");
    }

    const a = aSnap.data() as Profile;
    const d = dSnap.data() as Profile;

    const cap = (s:Stats):boolean=>{
      const inRange =
        s.hp<=100000&&
        s.atk<=10000&&
        s.def<=10000&&
        s.spd<=1000&&
        s.critChance>=0&&
        s.critChance<=0.9&&
        s.critMult>=1&&
        s.critMult<=5;
      return inRange;
    };

    if (!cap(a.stats)||!cap(d.stats)) {
      throw new HttpsError(
        "failed-precondition",
        "Stat cap violation"
      );
    }

    const seed = Math.floor(Math.random()*0x7fffffff);
    const result = resolve(a, d, seed);

    const duelRef = db.collection("duels").doc();
    await duelRef.set({
      duelId: duelRef.id,
      attackerId: attackerId,
      defenderId: defenderId,
      attackerSnap: a,
      defenderSnap: d,
      seed: seed,
      status: "resolved",
      winnerId: result.winnerId,
      rounds: result.turns.length,
      logSummary: result.turns,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
      resolvedAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    return {
      duelId: duelRef.id,
      winnerId: result.winnerId,
      rounds: result.turns.length,
    };
  }
);
