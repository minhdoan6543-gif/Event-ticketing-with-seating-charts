import { createBrowserRouter } from 'react-router-dom';
import App from './App.jsx';
import Login from './components/Login.jsx';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      {
        path: 'login',
        element: <Login />,
      }
      // Add other routes here later
    ]
  }
]);
