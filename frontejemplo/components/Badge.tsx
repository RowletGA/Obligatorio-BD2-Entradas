import { badgeTone, humanStatus } from "@/utils/format";

export const Badge = ({ value }: { value: string }) => (
  <span className={`badge capitalize ${badgeTone(value)}`}>{humanStatus(value)}</span>
);
