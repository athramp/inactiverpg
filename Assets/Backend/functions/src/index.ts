import { onCall, HttpsError } from "firebase-functions/v2/https";
import { initializeApp } from "firebase-admin/app";
import { getFirestore } from "firebase-admin/firestore";

initializeApp();
const db = getFirestore();

export const getServerTime = onCall(() => {
  return { epochSeconds: Math.floor(Date.now() / 1000) };
});

const CFG = { mobHp: 20, goldPerKill: 2, capHours: 14, eff: 0.9 };
const clamp = (n:number,a:number,b:number)=> Math.max(a, Math.min(n,b));
const killsFrom = (elapsedSec:number,dps:number)=> Math.max(0, Math.floor((elapsedSec * dps * CFG.eff) / CFG.mobHp));
export const setDps = onCall(async (req) => {
  const u = uid(req);
  const dps = Number(req.data?.dps);
  if (!Number.isFinite(dps) || dps <= 0 || dps > 1e6) {
    throw new HttpsError("invalid-argument","dps must be a positive number");
  }
  const ref = await ensurePlayer(u);
  await ref.set({ build: { dps } }, { merge: true });
  return { ok: true, dps };
});


const uid = (req:any)=> {
  if (!req.auth?.uid) throw new HttpsError("unauthenticated","Login required");
  return req.auth.uid as string;
};


async function ensurePlayer(u: string) {
  const ref = db.collection("players").doc(u);
  const snap = await ref.get();
  if (!snap.exists) {
    await ref.set({
      uid: u,
      currencies: { gold: 0, gems: 0 },
      build: { dps: 12 },
      idle: { last_seen_utc: Math.floor(Date.now()/1000), cap_hours: CFG.capHours }
    }, { merge: true });
  }
  return ref;
}

export const previewOfflineKills = onCall(async (req) => {
  try {
    const u = uid(req);
    const ref = await ensurePlayer(u);
    const now = Math.floor(Date.now()/1000);

    const doc = (await ref.get()).data() || {};
    const idle = (doc as any).idle || {};
    const build = (doc as any).build || {};

    const last = idle.last_seen_utc ?? now;
    const cap  = (idle.cap_hours ?? CFG.capHours) * 3600;
    const dps  = Number(build.dps ?? 12);

    const elapsed = clamp(now - last, 0, cap);
    const kills = killsFrom(elapsed, dps);
    const gold  = kills * CFG.goldPerKill;

    return { elapsedSeconds: elapsed, kills, gold };
  } catch (e:any) {
    console.error("previewOfflineKills error:", e);
    throw new HttpsError("internal", e?.message ?? "internal");
  }
});

export const claimOfflineLoot = onCall(async (req) => {
  try {
    const u = uid(req);
    const now = Math.floor(Date.now()/1000);
    const ref = await ensurePlayer(u);

    const result = await db.runTransaction(async tx => {
      const snap = await tx.get(ref);
      const doc:any = snap.data() || {};
      const idle = doc.idle || {};
      const build = doc.build || {};

      const last = idle.last_seen_utc ?? now;
      const cap  = (idle.cap_hours ?? CFG.capHours) * 3600;
      const dps  = Number(build.dps ?? 12);

      const elapsed = clamp(now - last, 0, cap);
      const kills = killsFrom(elapsed, dps);
      const gold  = kills * CFG.goldPerKill;

      const currencies = doc.currencies || { gold: 0, gems: 0 };
      currencies.gold = (currencies.gold || 0) + gold;

      tx.set(ref, { currencies, idle: { ...idle, last_seen_utc: now } }, { merge: true });
      return { elapsedSeconds: elapsed, kills, gold };
    });

    return result;
  } catch (e:any) {
    console.error("claimOfflineLoot error:", e);
    throw new HttpsError("internal", e?.message ?? "internal");
  }
});
