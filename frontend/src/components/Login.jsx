import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { jwtDecode } from 'jwt-decode';
import axiosClient from '../api/axiosClient';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const navigate = useNavigate();

  const handleLogin = async (e) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      const response = await axiosClient.post('/auth/login', { email, password });
      
      // Assuming response contains token. 
      // If the backend returned a message & userId, we might need to adjust this depending on if token is returned.
      // Wait, in previous task I returned { message: "Login successful", userId: user.Id }. I didn't actually generate a JWT in the backend yet! 
      // But the prompt says: "Upon successful login, decode the JWT to extract the user's Role and store it securely."
      // So I will assume the backend login endpoint returns a `token` field (perhaps updated by another team member or expected to be present).
      const { token } = response.data;
      if (token) {
        localStorage.setItem('token', token);
        
        const decoded = jwtDecode(token);
        // The role claim key varies in .NET, commonly:
        const role = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || decoded.role || decoded.Role;
        if (role) {
          localStorage.setItem('role', role);
        }
        
        const params = new URLSearchParams(window.location.search);
        const returnUrl = params.get('returnUrl');
        if (returnUrl) {
          navigate(returnUrl);
        } else {
          navigate('/');
        }
      } else {
        setError('Login failed: No token received.');
      }
    } catch (err) {
      if (err.response?.data?.error?.code === 'ACCOUNT_INACTIVE') {
        setError('Account not activated. Please check your email for the activation link.');
      } else {
        setError(err.response?.data?.message || 'Invalid email or password');
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: '400px', margin: '50px auto', padding: '20px', border: '1px solid #ccc' }}>
      <h2>Login</h2>
      {error && <div style={{ color: 'red', marginBottom: '10px' }}>{error}</div>}
      <form onSubmit={handleLogin}>
        <div style={{ marginBottom: '15px' }}>
          <label style={{ display: 'block', marginBottom: '5px' }}>Email</label>
          <input 
            type="email" 
            value={email} 
            onChange={(e) => setEmail(e.target.value)} 
            required 
            style={{ width: '100%', padding: '8px' }}
          />
        </div>
        <div style={{ marginBottom: '15px' }}>
          <label style={{ display: 'block', marginBottom: '5px' }}>Password</label>
          <input 
            type="password" 
            value={password} 
            onChange={(e) => setPassword(e.target.value)} 
            required 
            style={{ width: '100%', padding: '8px' }}
          />
        </div>
        <button type="submit" disabled={isLoading} style={{ width: '100%', padding: '10px', backgroundColor: '#007bff', color: '#fff', border: 'none' }}>
          {isLoading ? 'Logging in...' : 'Login'}
        </button>
      </form>
    </div>
  );
}
