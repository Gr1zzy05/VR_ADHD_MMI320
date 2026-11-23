import {
  createCipheriv,
  createDecipheriv,
  randomBytes,
  randomUUID,
  scryptSync,
} from "node:crypto";
import { noticeVersion } from "./model.ts";

export function makeBinding(
  participantId: string,
  endpoint: string,
  days: number,
  issuedAt: Date,
) {
  return {
    sessionId: randomUUID(),
    participantId,
    endpoint,
    token: randomBytes(32).toString("base64url"),
    expiresAt: new Date(issuedAt.getTime() + days * 86400000).toISOString(),
    noticeVersion,
  };
}

export type DevicePack = {
  participantCode: string;
  bindings: ReturnType<typeof makeBinding>[];
};

export function protectDevicePacks(serverSecret: string) {
  const key = scryptSync(serverSecret, "focus-shift-device-pairing:v1", 32);
  return {
    seal(pack: DevicePack, code: string) {
      const nonce = randomBytes(12);
      const cipher = createCipheriv("aes-256-gcm", key, nonce);
      cipher.setAAD(Buffer.from(code));
      const ciphertext = Buffer.concat([
        cipher.update(JSON.stringify(pack), "utf8"),
        cipher.final(),
      ]);
      return Buffer.concat([nonce, cipher.getAuthTag(), ciphertext]).toString(
        "base64url",
      );
    },
    open(sealed: string, code: string) {
      const bytes = Buffer.from(sealed, "base64url");
      const decipher = createDecipheriv(
        "aes-256-gcm",
        key,
        bytes.subarray(0, 12),
      );
      decipher.setAAD(Buffer.from(code));
      decipher.setAuthTag(bytes.subarray(12, 28));
      return JSON.parse(
        Buffer.concat([
          decipher.update(bytes.subarray(28)),
          decipher.final(),
        ]).toString("utf8"),
      ) as DevicePack;
    },
  };
}
