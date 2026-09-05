import { useState, type CSSProperties } from 'react';

export function TeamLogo({ team, size = 32, lazy = true }: {
  team: { teamName: string; clubLogoUrl?: string | null }; size?: number; lazy?: boolean;
}) {
  const [failedUrl, setFailedUrl] = useState<string>();
  const alt = `Logo del club de ${team.teamName}`;
  return team.clubLogoUrl && failedUrl !== team.clubLogoUrl ?
    <img className="team-logo" width={size} height={size} src={team.clubLogoUrl} alt={alt}
      loading={lazy ? 'lazy' : 'eager'} onError={() => setFailedUrl(team.clubLogoUrl!)} /> :
    <span className="team-logo fallback" style={{ '--team-logo-size': `${size}px` } as CSSProperties} role="img"
      aria-label={`${team.teamName}, sin logo`}>{team.teamName.slice(0, 2).toUpperCase()}</span>;
}
