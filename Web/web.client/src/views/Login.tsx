import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../hooks/useAuth';
import './Login.css';

const Login: React.FC = () => {
  const { step, emailInput, remember, loading, error, setEmailInput, setRemember, requestCode, verifyCode } = useAuth();
  const navigate = useNavigate();
  const [code, setCode] = useState('');

  const handleEmailSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    requestCode();
  };

  const handleCodeSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    verifyCode(code.trim());
  };

  useEffect(() => {
    if (step === 'ready') {
      // Small delay to allow UI to show success momentarily if desired.
      const t = setTimeout(() => {
        navigate('/railmap', { replace: true });
      }, 200);
      return () => clearTimeout(t);
    }
  }, [step, navigate]);

  return (
    <div className="login-page">
      <div className="login-card" role="dialog" aria-labelledby="loginTitle">
        <h1 id="loginTitle" className="login-title">Sign In</h1>
        <p className="login-subtitle">Access the rail map and telemetry console.</p>
        {error && <div className="login-error" role="alert">{error}</div>}
        {step === 'email' && (
          <form onSubmit={handleEmailSubmit} noValidate>
            <label className="login-form-label" htmlFor="emailInput">Email Address</label>
            <input
              id="emailInput"
              className="login-input"
              type="email"
              required
              autoComplete="email"
              value={emailInput}
              onChange={e => setEmailInput(e.target.value)}
              placeholder="you@example.com"
            />
            <label className="login-remember">
              <input
                type="checkbox"
                checked={remember}
                onChange={e => setRemember(e.target.checked)}
              />
              Remember me (1 year)
            </label>
            <div className="login-info">We'll send a 6-digit code to this email to verify it's you.</div>
            <button type="submit" className="login-action-btn" disabled={loading}>{loading ? 'Sending…' : 'Send Code'}</button>
          </form>
        )}
        {step === 'code' && (
          <form onSubmit={handleCodeSubmit} noValidate>
            <div className="login-info">Enter the 6-digit code sent to <strong>{emailInput}</strong>.</div>
            <input
              className="login-input"
              type="text"
              inputMode="numeric"
              pattern="[0-9]{6}"
              maxLength={6}
              required
              value={code}
              onChange={e => setCode(e.target.value.replace(/[^0-9]/g, ''))}
              placeholder="123456"
            />
            <button type="submit" className="login-action-btn" disabled={loading || code.length !== 6}>{loading ? 'Verifying…' : 'Verify Code'}</button>
            <div className="login-resend-wrap">
              Didn't get it? <button type="button" onClick={requestCode} className="login-resend-btn">Resend code</button>
            </div>
          </form>
        )}
        {step === 'ready' && (
          <div className="login-ready-msg">Authenticated. Reload page if not redirected.</div>
        )}
      </div>
    </div>
  );
};

export default Login;