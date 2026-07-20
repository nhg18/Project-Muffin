using System;
using UnityEngine;

namespace UI.Popup
{
    public abstract class Popup : MonoBehaviour
    {
        public void Open()
        {
            OnOpen();
        }
        
        public void Close()
        {
            OnClose();
        }
        
        protected virtual void OnOpen()
        {
            
        }

        protected virtual void OnClose()
        {
            
        }
    }
}
