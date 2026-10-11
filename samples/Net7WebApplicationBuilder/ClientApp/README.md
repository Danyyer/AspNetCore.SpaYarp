# ClientApp

Angular application served by the Vite based Angular dev server (`@angular/build:dev-server`, esbuild).
It is used by an ASP.NET Core host with [AspNetCore.SpaYarp](../../../Readme.md), which starts it with `npm start`
(or reuses it if it already runs) and forwards the requests that the host can't handle to it.

Requires Node.js `^22.22.3`, `^24.15.0` or newer (see `engines` in `package.json`).

```
npm install
npm start      # ng serve on https://localhost:44478/ (uses the ASP.NET Core dev certificate)
npm test       # unit tests (Vitest)
npm run build  # production build into dist/
```
