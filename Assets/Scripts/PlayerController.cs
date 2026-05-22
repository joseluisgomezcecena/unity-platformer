using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//added dash.
public class PlayerController : MonoBehaviour
{

    public static PlayerController instance;

    public float boundeForce;

    private void Awake()
    {
        instance = this;

        // Unity does not apply script defaults to fields added after a component is already serialized.
        // Patch zero/None values so the dash always works out of the box.
        if (dashKey == KeyCode.None) dashKey = KeyCode.RightShift;
        if (dashDuration <= 0f) dashDuration = 0.15f;
        if (dashWidthMultiplier <= 0f) dashWidthMultiplier = 5f;
        if (dashCooldown < 0f) dashCooldown = 0.5f;
    }

    [Header("Movement - Ground")]
    public float walkSpeed = 8f;
    public float runSpeed = 14f;
    public float acceleration = 25f;        // Snappy acceleration like SMW
    public float runAcceleration = 30f;     // Even snappier when running
    public float deceleration = 8f;         // Slower decel = more slide (SMW feel)
    public float drag = 6f;                 // Less drag = more momentum
    public float turnaroundMultiplier = 3f;  // How much faster to change directions
    
    [Header("Movement - Air")]
    public float airControlMultiplier = 0.9f;  // Nearly full air control like SMW
    public float airDrag = 3f;                 // Less drag in air
    
    private bool isRunning;
    private float speedWhenJumped; // Preserve momentum from jump

    [Header("Jumping")]
    public float jumpForce = 16f;
    public float runJumpForce = 24f;        // Significantly higher than walk jump (50% more)
    public float runSpeedThreshold = 0.7f;  // % of runSpeed needed to count as "running" for jump
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.15f;
    public float jumpCutMultiplier = 0.4f;  // More responsive variable jump height
    
    [Header("Gravity")]
    public float fallGravityMultiplier = 2.5f;  // Fall faster than rise (SMW feel)
    public float maxFallSpeed = 25f;            // Terminal velocity
    
    [Header("References")]
    public Rigidbody2D theRB;
    public Transform groundCheckPoint;
    public LayerMask whatIsGround;
    
    [Header("Wall Detection")]
    public Transform wallCheckPoint;         // Assign in Inspector (front of player)
    public float wallCheckDistance = 0.1f;   // How far to check for walls
    private bool isTouchingWall;

    [Header("Dash")]
    public KeyCode dashKey = KeyCode.RightShift;
    public float dashWidthMultiplier = 5f;   // Dash covers N times player width
    public float dashDuration = 0.15f;
    public float dashCooldown = 0.5f;        // Extra time AFTER the dash ends before you can dash again
    public bool dashCancelsGravity = true;

    private float dashCounter;
    private float dashCooldownCounter;
    private float dashDirection;
    private Collider2D theCol;


    //adding knockback
    public float knockbackLength; 
    public float knockbackForce;
    private float knockbackCounter; 



    
    private Vector2 currentVelocity;
    private bool isGrounded;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool wasRunningWhenBuffered; // Store running state when jump was PRESSED
    private bool isJumping;
    private bool wasRunningWhenJumped;
    private bool canDoubleJump;
    private Animator anim;
    private SpriteRenderer theSR;

    void Start()
    {
        currentVelocity = Vector2.zero;
        anim = GetComponent<Animator>();
        theSR = GetComponent<SpriteRenderer>();
        theCol = GetComponent<Collider2D>();
        
        // Validate jump force values
        ValidateJumpForces();
    }
    
    private void OnValidate()
    {
        // This runs in the editor when values change
        ValidateJumpForces();
    }
    
    private void ValidateJumpForces()
    {
        if (runJumpForce <= jumpForce)
        {
            Debug.LogError($"[PlayerController] ERROR: runJumpForce ({runJumpForce}) should be HIGHER than jumpForce ({jumpForce})! " +
                          "Right-click the component and select 'Reset' to fix, or manually set runJumpForce higher.");
        }
        else
        {
            Debug.Log($"[PlayerController] Jump forces OK - Walk: {jumpForce}, Run: {runJumpForce} (difference: +{runJumpForce - jumpForce})");
        }
    }

    void Update()
    {
        if (dashCooldownCounter > 0) dashCooldownCounter -= Time.deltaTime;

        if (knockbackCounter > 0)
        {
            knockbackCounter -= Time.deltaTime;
            //theRB.velocity = new Vector2(knockbackForce, theRB.velocity.y);
            if(!theSR.flipX)
            {
                theRB.velocity = new Vector2(-knockbackForce, theRB.velocity.y);
            }
            else
            {
                theRB.velocity = new Vector2(knockbackForce, theRB.velocity.y);
            }
        }
        else if (dashCounter > 0)
        {
            UpdateDash();
            HandleAnimations();
        }
        else
        {
            // Ground Check
            bool wasGrounded = isGrounded;
            isGrounded = Physics2D.OverlapCircle(groundCheckPoint.position, 0.2f, whatIsGround);

            // Wall Check - detect if we're touching a wall horizontally
            CheckWallCollision();

            // Just landed - reset states
            if (isGrounded && !wasGrounded)
            {
                isJumping = false;
                canDoubleJump = true;
            }

            // Try to start a dash before normal movement runs
            if (Input.GetKeyDown(dashKey) && dashCooldownCounter <= 0)
            {
                StartDash();
                HandleAnimations();
                return;
            }

            HandleHorizontalMovement();
            HandleCoyoteTime();
            HandleJumpBuffer();
            HandleJumping();
            HandleGravity();
            HandleSpriteFlip();
            HandleAnimations();
        }

    }

    private void CheckWallCollision()
    {
        // Determine which direction we're moving/facing
        float moveDirection = Mathf.Sign(currentVelocity.x);
        
        if (Mathf.Abs(currentVelocity.x) < 0.1f)
        {
            isTouchingWall = false;
            return;
        }
        
        // Cast a ray in the direction of movement to detect walls
        Vector2 rayOrigin = wallCheckPoint != null ? (Vector2)wallCheckPoint.position : (Vector2)transform.position;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * moveDirection, wallCheckDistance, whatIsGround);
        
        isTouchingWall = hit.collider != null;
        
        // If we hit a wall, immediately stop horizontal momentum
        if (isTouchingWall)
        {
            currentVelocity.x = 0f;
            theRB.velocity = new Vector2(0f, theRB.velocity.y);
        }
    }

    private void HandleHorizontalMovement()
    {
        float input = Input.GetAxisRaw("Horizontal");
        
        // Check if running (RightShift is now reserved for dash)
        isRunning = Input.GetKey(KeyCode.LeftShift);
        
        // Determine target speed - if in air, use the speed cap from when we jumped for better momentum
        float targetMaxSpeed;
        if (!isGrounded && isJumping)
        {
            // In air: allow the speed we had when jumping, but cap at current run/walk speed
            targetMaxSpeed = Mathf.Max(isRunning ? runSpeed : walkSpeed, Mathf.Abs(speedWhenJumped));
        }
        else
        {
            targetMaxSpeed = isRunning ? runSpeed : walkSpeed;
        }
        
        float currentAcceleration = isRunning ? runAcceleration : acceleration;
        float currentDrag = isGrounded ? drag : airDrag;
        
        // Apply air control multiplier when not grounded
        if (!isGrounded)
        {
            currentAcceleration *= airControlMultiplier;
        }
        
        float targetSpeed = targetMaxSpeed * input;

        if (input != 0 && Mathf.Sign(input) != Mathf.Sign(currentVelocity.x) && Mathf.Abs(currentVelocity.x) > 0.1f)
        {
            // Turning around - use BOOSTED deceleration for snappy direction changes
            float decelerationToUse = isGrounded ? deceleration * turnaroundMultiplier : deceleration * turnaroundMultiplier * airControlMultiplier;
            currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0, decelerationToUse * Time.deltaTime);
            
            // If we've nearly stopped, immediately start accelerating in new direction
            if (Mathf.Abs(currentVelocity.x) < 0.5f)
            {
                // Snap to zero and accelerate faster in new direction
                currentVelocity.x = 0f;
                currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, targetSpeed, currentAcceleration * turnaroundMultiplier * Time.deltaTime);
            }
        }
        else if (input != 0)
        {
            // Normal acceleration
            currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, targetSpeed, currentAcceleration * Time.deltaTime);
        }
        else
        {
            // No input - apply drag (slide to stop)
            currentVelocity.x = Mathf.MoveTowards(currentVelocity.x, 0, currentDrag * Time.deltaTime);
        }

        theRB.velocity = new Vector2(currentVelocity.x, theRB.velocity.y);
    }

    private void HandleCoyoteTime()
    {
        if (isGrounded)
        {
            coyoteTimeCounter = coyoteTime;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }
    }

    private void HandleJumpBuffer()
    {
        if (Input.GetButtonDown("Jump"))
        {
            jumpBufferCounter = jumpBufferTime;
            // Store running state NOW when button is pressed, not when jump executes
            // Use BOTH shift key AND speed check for reliability
            bool holdingRun = Input.GetKey(KeyCode.LeftShift);
            bool movingFast = Mathf.Abs(currentVelocity.x) >= runSpeed * runSpeedThreshold;
            wasRunningWhenBuffered = holdingRun || movingFast;
            
            Debug.Log($"Jump buffered! HoldingShift: {holdingRun}, Speed: {Mathf.Abs(currentVelocity.x)}, Threshold: {runSpeed * runSpeedThreshold}, WillRunJump: {wasRunningWhenBuffered}");
        }
        else
        {
            jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void HandleJumping()
    {
        // Check if we should execute a jump
        if (jumpBufferCounter > 0f)
        {
            // Can jump if grounded OR within coyote time
            if (isGrounded || coyoteTimeCounter > 0f)
            {
                // Use the running state from when jump was PRESSED, not current state
                ExecuteJump(wasRunningWhenBuffered);
            }
            else if (canDoubleJump)
            {
                ExecuteDoubleJump();
            }
        }

        // Variable jump height - cut jump short if button released early
        if (Input.GetButtonUp("Jump") && isJumping && theRB.velocity.y > 0)
        {
            theRB.velocity = new Vector2(theRB.velocity.x, theRB.velocity.y * jumpCutMultiplier);
        }
    }

    private void ExecuteJump(bool running)
    {
        float currentJumpForce = running ? runJumpForce : jumpForce;
        wasRunningWhenJumped = running;
        speedWhenJumped = Mathf.Abs(currentVelocity.x); // Remember speed for air momentum
        
        theRB.velocity = new Vector2(theRB.velocity.x, currentJumpForce);
        
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        isJumping = true;
        
        Debug.Log($"Jump! Running: {running}, Force: {currentJumpForce}, Speed: {speedWhenJumped}");
    }

    private void ExecuteDoubleJump()
    {
        float currentJumpForce = wasRunningWhenJumped ? runJumpForce : jumpForce;
        
        theRB.velocity = new Vector2(theRB.velocity.x, currentJumpForce);
        
        canDoubleJump = false;
        jumpBufferCounter = 0f;
        
        Debug.Log($"Double Jump! Force: {currentJumpForce}");
    }

    private void HandleGravity()
    {
        // Apply extra gravity when falling for snappier descent (SMW feel)
        if (!isGrounded && theRB.velocity.y < 0)
        {
            // Increase gravity on the way down
            theRB.velocity += Vector2.up * Physics2D.gravity.y * (fallGravityMultiplier - 1) * Time.deltaTime;
            
            // Clamp to max fall speed
            if (theRB.velocity.y < -maxFallSpeed)
            {
                theRB.velocity = new Vector2(theRB.velocity.x, -maxFallSpeed);
            }
        }
    }

    private void HandleSpriteFlip()
    {
        if (theRB.velocity.x < -0.1f)
        {
            theSR.flipX = true;
        }
        else if (theRB.velocity.x > 0.1f)
        {
            theSR.flipX = false;
        }
    }

    private void HandleAnimations()
    {
        anim.SetBool("isGrounded", isGrounded);
        anim.SetFloat("moveSpeed", Mathf.Abs(theRB.velocity.x));
        
        if (HasParameter(anim, "isRunning"))
        {
            anim.SetBool("isRunning", isRunning && Mathf.Abs(theRB.velocity.x) > walkSpeed * 0.5f);
        }
    }
    
    private bool HasParameter(Animator animator, string paramName)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }


    public void Knockback()
    {
        knockbackCounter = knockbackLength;

        // Reset the stored velocity so player doesn't auto-resume movement after knockback
        currentVelocity.x = 0f;

        //theRB.velocity = new Vector2(knockbackForce, theRB.velocity.y);
        theRB.velocity = new Vector2(0f, knockbackForce);

        anim.SetTrigger("hurt");
    }



    public void Bounce()
    {
        theRB.velocity = new Vector2(theRB.velocity.x, boundeForce);
    }



    private void StartDash()
    {
        float input = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(input) > 0.1f)
            dashDirection = Mathf.Sign(input);
        else
            dashDirection = theSR.flipX ? -1f : 1f;

        dashCounter = dashDuration;
        dashCooldownCounter = dashCooldown + dashDuration;

        // Lock visual facing to dash direction
        theSR.flipX = dashDirection < 0;

        // Clear any pending jump-buffer so dash doesn't end with a stale jump
        jumpBufferCounter = 0f;

        Debug.Log($"Dash! Direction: {dashDirection}");
    }

    private void UpdateDash()
    {
        dashCounter -= Time.deltaTime;

        float playerWidth = theCol != null ? theCol.bounds.size.x : 1f;
        float dashSpeed = (playerWidth * dashWidthMultiplier) / dashDuration;

        // Cancel dash if we hit a wall in the dash direction
        Vector2 rayOrigin = wallCheckPoint != null ? (Vector2)wallCheckPoint.position : (Vector2)transform.position;
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * dashDirection, wallCheckDistance, whatIsGround);
        if (hit.collider != null)
        {
            EndDash();
            return;
        }

        float verticalVel = dashCancelsGravity ? 0f : theRB.velocity.y;
        theRB.velocity = new Vector2(dashDirection * dashSpeed, verticalVel);

        if (dashCounter <= 0)
        {
            EndDash();
        }
    }

    private void EndDash()
    {
        dashCounter = 0f;

        // Clamp exit speed to running cap so we don't slide forever
        float exitX = Mathf.Clamp(theRB.velocity.x, -runSpeed, runSpeed);
        currentVelocity.x = exitX;
        theRB.velocity = new Vector2(exitX, theRB.velocity.y);
    }

}