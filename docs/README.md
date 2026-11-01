# User Guide

The Train Tracker user guide, built with [Docusaurus](https://docusaurus.io/). The guide content is in `docs/userguide.mdx` and its images are in `static/img/userguide/`.

## Local Development

```bash
npm install
npm start
```

This starts a local development server at `http://localhost:3000/docs/`. Most changes are reflected live without restarting the server.

## Build

```bash
npm run build
```

This generates static content into the `build` directory.

## Deployment

There is no separate deployment. Publishing `Web/Web.Server` builds this site and copies it into `wwwroot/docs`, so the guide is served by the app at `/docs/` and ships with every beta and production deploy.
