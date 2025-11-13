import path from 'path';
/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  eslint: { ignoreDuringBuilds: true },
  // Ensure Next uses the Copilot UI folder as its root for env resolution
  outputFileTracingRoot: path.join(__dirname),
  env: {
    // Hardcode API URL to avoid multi-lockfile root confusion during dev
    NEXT_PUBLIC_API_URL: 'http://localhost:5068'
  }
};

export default nextConfig;
