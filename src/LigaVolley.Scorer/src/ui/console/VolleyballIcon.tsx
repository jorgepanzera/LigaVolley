export function VolleyballIcon({ title }: { title?: string }) {
  return (
    <svg
      className="volleyball-icon"
      viewBox="0 0 32 32"
      role={title ? 'img' : undefined}
      aria-hidden={title ? undefined : true}
    >
      {title && <title>{title}</title>}
      <circle cx="16" cy="16" r="13" />
      <path d="M16 3c2.8 4.4 3.6 8.7 2.2 12.8M6.2 7.2c5.2-.2 9.2 1.4 12 4.8M3.4 18.7c4.4-2.7 8.7-3.4 12.8-2M10.6 27.8c-.7-5.1.5-9.2 3.7-12.3M23.8 25c-4.8-2-8-5-9.5-9.5M28.8 12.6c-3.5 3.8-7.4 5.7-12 5.8" />
    </svg>
  );
}
